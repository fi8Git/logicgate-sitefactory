using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

internal abstract class AtomicWriteSession : IDisposable
{
    private const string TemporaryPrefix = ".sitefactory-";
    private const string TemporarySuffix = ".tmp";
    private const int RandomNameLength = 32;
    internal static int TemporaryNameLength => TemporaryPrefix.Length + RandomNameLength + TemporarySuffix.Length;

    protected SafeFileHandle? TemporaryHandle { get; set; }

    protected string TemporaryName { get; } = TemporaryPrefix + RandomNumberGenerator.GetHexString(RandomNameLength) + TemporarySuffix;

    protected bool Committed { get; set; }

    public static AtomicWriteSession Open(GenerationRoot root, string logicalPath, CancellationToken cancellationToken,
        bool createDirectories = true)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsAtomicWriteSession.Open(root, logicalPath, cancellationToken, createDirectories);
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxAtomicWriteSession.Open(root, logicalPath, cancellationToken, createDirectories);
        }

        throw new PlatformNotSupportedException("Atomic generation writes support Windows and Linux only.");
    }

    public FileStream OpenStream() => new(new SafeFileHandle(TemporaryHandle!.DangerousGetHandle(), ownsHandle: false),
        FileAccess.Write, bufferSize: 4096, isAsync: false);

    // Null means a directory component is absent, never an access failure or a followed link.
    public static AtomicWriteSession? OpenExisting(GenerationRoot root, string logicalPath, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows()) return WindowsAtomicWriteSession.TryOpen(root, logicalPath, createDirectories: false, cancellationToken);
        if (OperatingSystem.IsLinux()) return LinuxAtomicWriteSession.TryOpen(root, logicalPath, createDirectories: false, cancellationToken);
        throw new PlatformNotSupportedException("Atomic generation writes support Windows and Linux only.");
    }

    /// <summary>Inspects metadata without reading or hashing content; links and special files are rejected.</summary>
    public abstract bool DestinationExists();

    /// <summary>Reads a small control document through the same no-follow handle as file inspection.</summary>
    internal async Task<byte[]?> ReadControlDocumentAsync(int maximumLength, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        VerifyDirectories();
        using SafeFileHandle? handle = OpenDestinationForRead();
        if (handle is null) return null;
        using FileStream stream = new(new SafeFileHandle(handle.DangerousGetHandle(), ownsHandle: false),
            FileAccess.Read, bufferSize: 4096, isAsync: false);
        if (stream.Length > maximumLength) throw new IOException("The generation control document is too large.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        if (stream.Length != bytes.Length) throw new IOException("The generation control document changed while being read.");
        VerifyDirectories();
        return bytes;
    }

    public async Task<AtomicFileSnapshot?> ReadDestinationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SafeFileHandle? handle = OpenDestinationForRead();
        if (handle is null) return null;
        try
        {
            string identity = GetDestinationIdentity(handle);
            using FileStream stream = new(new SafeFileHandle(handle.DangerousGetHandle(), ownsHandle: false),
                FileAccess.Read, bufferSize: 4096, isAsync: false);
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new(handle, identity, new(Convert.ToHexString(hash)));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    protected abstract SafeFileHandle? OpenDestinationForRead();

    protected abstract string GetDestinationIdentity(SafeFileHandle handle);

    public abstract void VerifyDirectories();

    // Read-only enumeration anchored to the same verified parent as the native file reads.
    // physicalParent is used only on Windows, where every ancestor is held without delete sharing.
    internal abstract IEnumerable<(string Name, FileAttributes Attributes)> EnumerateParent(string physicalParent);

    public abstract void CreateTemporary();

    public abstract void Commit(AtomicFileWriteMode mode);

    public abstract void Dispose();
}
