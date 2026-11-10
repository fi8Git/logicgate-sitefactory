using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

[SupportedOSPlatform("windows")]
internal sealed class WindowsAtomicWriteSession : AtomicWriteSession
{
    private readonly List<SafeFileHandle> directories = [];
    private readonly string destinationName;
    private IntPtr securityDescriptor;

    private WindowsAtomicWriteSession(string destinationName) => this.destinationName = destinationName;

    private SafeFileHandle Parent => directories[^1];

    internal new static WindowsAtomicWriteSession Open(GenerationRoot root, string logicalPath, CancellationToken cancellationToken,
        bool createDirectories = true) =>
        TryOpen(root, logicalPath, createDirectories, cancellationToken) ?? throw new IOException("A generation directory is absent.");

    internal static WindowsAtomicWriteSession? TryOpen(GenerationRoot root, string logicalPath, bool createDirectories,
        CancellationToken cancellationToken)
    {
        WindowsAtomicWriteSession session = new(logicalPath.Split('/')[^1]);
        try
        {
            session.securityDescriptor = WindowsFileSystem.PrivateSecurityDescriptor();
            string volume = Path.GetPathRoot(root.FullPath)!;
            session.directories.Add(WindowsFileSystem.OpenVolume(volume));
            string[] rootSegments = root.FullPath[volume.Length..].Split(Path.DirectorySeparatorChar);
            for (int index = 0; index < rootSegments.Length; index++)
            {
                if (!session.OpenDirectory(rootSegments[index], create: createDirectories && index == rootSegments.Length - 1, cancellationToken))
                {
                    session.Dispose();
                    return null;
                }
            }

            foreach (string segment in logicalPath.Split('/').SkipLast(1))
            {
                if (!session.OpenDirectory(segment, create: createDirectories, cancellationToken))
                {
                    session.Dispose();
                    return null;
                }
            }

            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public override void VerifyDirectories()
    {
        // No FILE_SHARE_DELETE on any ancestor: renaming/replacing it is denied for the session lifetime.
        foreach (SafeFileHandle directory in directories) WindowsFileSystem.RejectReparsePoint(directory);
    }

    internal override IEnumerable<(string Name, FileAttributes Attributes)> EnumerateParent(string physicalParent)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(physicalParent))
        {
            // GetFileAttributes reports the link itself, not its target. Ancestors are pinned by this session.
            yield return (Path.GetFileName(path), File.GetAttributes(path));
        }
    }

    public override bool DestinationExists()
    {
        using SafeFileHandle? handle = OpenDestination(readContent: false);
        return handle is not null;
    }

    protected override SafeFileHandle? OpenDestinationForRead() => OpenDestination(readContent: true);

    private SafeFileHandle? OpenDestination(bool readContent)
    {
        int status = WindowsFileSystem.OpenChild(Parent, destinationName, directory: false, create: false,
            temporary: false, IntPtr.Zero, out SafeFileHandle handle, readContent);
        if (status == WindowsFileSystem.NameNotFound)
        {
            handle.Dispose();
            return null;
        }

        try
        {
            WindowsFileSystem.Check(status, "Opening destination without following links");
            WindowsFileSystem.RejectReparsePoint(handle);
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    protected override string GetDestinationIdentity(SafeFileHandle handle) => WindowsFileSystem.FileIdentity(handle);

    public override void CreateTemporary()
    {
        int status = WindowsFileSystem.OpenChild(Parent, TemporaryName, directory: false, create: true,
            temporary: true, securityDescriptor, out SafeFileHandle handle);
        if (status < 0)
        {
            handle.Dispose();
            WindowsFileSystem.Check(status, "Creating exclusive temporary file");
        }

        TemporaryHandle = handle;
    }

    public override void Commit(AtomicFileWriteMode mode)
    {
        int status = WindowsFileSystem.OpenChild(Parent, destinationName, directory: false, create: false,
            temporary: false, IntPtr.Zero, out SafeFileHandle destination);
        using (destination)
        {
            if (status != WindowsFileSystem.NameNotFound)
            {
                WindowsFileSystem.Check(status, "Inspecting destination without following links");
                WindowsFileSystem.RejectReparsePoint(destination);
            }
        }

        WindowsFileSystem.Rename(TemporaryHandle!, Parent, destinationName, mode == AtomicFileWriteMode.CreateOrReplace);
        Committed = true;
    }

    public override void Dispose()
    {
        if (TemporaryHandle is not null && !Committed)
        {
            try
            {
                WindowsFileSystem.DeleteOnClose(TemporaryHandle);
            }
            catch (IOException)
            {
                // Cleanup is best effort; never mask the write/cancellation error or delete by an untrusted path.
            }
        }

        TemporaryHandle?.Dispose();
        TemporaryHandle = null;
        for (int index = directories.Count - 1; index >= 0; index--) directories[index].Dispose();
        directories.Clear();
        if (securityDescriptor != IntPtr.Zero)
        {
            WindowsFileSystem.FreeSecurityDescriptor(securityDescriptor);
            securityDescriptor = IntPtr.Zero;
        }
    }

    private bool OpenDirectory(string name, bool create, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int status = WindowsFileSystem.OpenChild(Parent, name, directory: true, create,
            temporary: false, securityDescriptor, out SafeFileHandle handle);
        if (!create && status == WindowsFileSystem.NameNotFound)
        {
            handle.Dispose();
            return false;
        }

        try
        {
            WindowsFileSystem.Check(status, "Opening anchored generation directory");
            WindowsFileSystem.RejectReparsePoint(handle);
            directories.Add(handle);
            return true;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }
}
