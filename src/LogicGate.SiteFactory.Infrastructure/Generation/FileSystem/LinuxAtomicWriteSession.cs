using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

[SupportedOSPlatform("linux")]
internal sealed class LinuxAtomicWriteSession : AtomicWriteSession
{
    private readonly List<DirectoryAnchor> directories = [];
    private readonly uint effectiveUserId = LinuxFileSystem.EffectiveUserId();
    private readonly string destinationName;
    private LinuxFileSystem.Statx temporaryIdentity;
    private ulong rootMountId;

    private LinuxAtomicWriteSession(string destinationName) => this.destinationName = destinationName;

    private SafeFileHandle Parent => directories[^1].Handle;

    internal new static LinuxAtomicWriteSession Open(GenerationRoot root, string logicalPath, CancellationToken cancellationToken,
        bool createDirectories = true) =>
        TryOpen(root, logicalPath, createDirectories, cancellationToken) ?? throw new IOException("A generation directory is absent.");

    internal static LinuxAtomicWriteSession? TryOpen(GenerationRoot root, string logicalPath, bool createDirectories,
        CancellationToken cancellationToken)
    {
        LinuxAtomicWriteSession session = new(logicalPath.Split('/')[^1]);
        try
        {
            int descriptor = LinuxFileSystem.Open("/", LinuxFileSystem.DirectoryFlags, 0);
            if (descriptor < 0) throw LinuxFileSystem.Error("Opening volume root", Marshal.GetLastPInvokeError());
            session.AddDirectory("/", LinuxFileSystem.Handle(descriptor), insideRoot: false);

            string[] rootSegments = root.FullPath[1..].Split('/');
            for (int index = 0; index < rootSegments.Length; index++)
            {
                bool isRoot = index == rootSegments.Length - 1;
                if (!session.OpenDirectory(rootSegments[index], create: isRoot && createDirectories, insideRoot: isRoot, cancellationToken))
                {
                    session.Dispose();
                    return null;
                }
                if (isRoot)
                {
                    session.rootMountId = session.directories[^1].Identity.MountId;
                    LinuxFileSystem.RequireSupportedFileSystem(session.Parent);
                }
            }

            foreach (string segment in logicalPath.Split('/').SkipLast(1))
            {
                if (!session.OpenDirectory(segment, create: createDirectories, insideRoot: true, cancellationToken))
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
        for (int index = 0; index < directories.Count; index++)
        {
            DirectoryAnchor directory = directories[index];
            CheckPermissions(LinuxFileSystem.Metadata(directory.Handle), directory.InsideRoot);
            if (index == 0) continue;

            int descriptor = LinuxFileSystem.OpenAt(directories[index - 1].Handle, directory.Name, LinuxFileSystem.DirectoryFlags, 0);
            if (descriptor < 0) throw LinuxFileSystem.Error("Rechecking anchored directory", Marshal.GetLastPInvokeError());
            using SafeFileHandle current = LinuxFileSystem.Handle(descriptor);
            if (!directory.Identity.SameFile(LinuxFileSystem.Metadata(current)))
            {
                throw new IOException("A generation directory was replaced during the write.");
            }
        }
    }

    public override bool DestinationExists()
    {
        LinuxFileSystem.Statx? metadata = LinuxFileSystem.MetadataAt(Parent, destinationName);
        if (metadata is null) return false;
        if ((metadata.Value.Mode & 0xF000) != 0x8000 || metadata.Value.MountId != rootMountId)
        {
            throw new IOException("The destination must be a regular file on the generation filesystem.");
        }

        return true;
    }

    internal override IEnumerable<(string Name, FileAttributes Attributes)> EnumerateParent(string physicalParent)
    {
        // procfs addresses our already-open directory, never a path from the repository.
        // Keep the session alive until enumeration completes. No pathname-based recursive walk.
        string directory = FormattableString.Invariant($"/proc/self/fd/{Parent.DangerousGetHandle()}");
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            string name = Path.GetFileName(path);
            LinuxFileSystem.Statx metadata = LinuxFileSystem.MetadataAt(Parent, name)
                ?? throw new IOException("A repository entry disappeared during enumeration.");
            FileAttributes attributes = (metadata.Mode & 0xF000) switch
            {
                0x8000 => FileAttributes.Normal,
                0x4000 => FileAttributes.Directory,
                0xA000 => FileAttributes.ReparsePoint,
                _ => FileAttributes.Device,
            };
            yield return (name, attributes);
        }
    }

    protected override SafeFileHandle? OpenDestinationForRead()
    {
        // NONBLOCK avoids blocking on a FIFO before its type can be rejected; ordinary files remain blocking.
        int descriptor = LinuxFileSystem.OpenAt(Parent, destinationName,
            0x800 | LinuxFileSystem.NoFollow | LinuxFileSystem.CloseOnExec, 0); // RDONLY, NONBLOCK
        if (descriptor < 0)
        {
            int error = Marshal.GetLastPInvokeError();
            if (error == 2) return null;
            throw LinuxFileSystem.Error("Opening destination without following links", error);
        }

        SafeFileHandle handle = LinuxFileSystem.Handle(descriptor);
        try
        {
            LinuxFileSystem.Statx metadata = LinuxFileSystem.Metadata(handle);
            if ((metadata.Mode & 0xF000) != 0x8000 || metadata.MountId != rootMountId)
            {
                throw new IOException("The destination must be a regular file on the generation filesystem.");
            }

            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    protected override string GetDestinationIdentity(SafeFileHandle handle)
    {
        LinuxFileSystem.Statx metadata = LinuxFileSystem.Metadata(handle);
        return FormattableString.Invariant($"{metadata.MountId}:{metadata.DeviceMajor}:{metadata.DeviceMinor}:{metadata.Inode}");
    }

    public override void CreateTemporary()
    {
        int descriptor = LinuxFileSystem.OpenAt(Parent, TemporaryName,
            1 | 0x40 | 0x80 | LinuxFileSystem.NoFollow | LinuxFileSystem.CloseOnExec, 0x180); // WRONLY, CREAT, EXCL, 0600
        if (descriptor < 0) throw LinuxFileSystem.Error("Creating exclusive temporary file", Marshal.GetLastPInvokeError());
        TemporaryHandle = LinuxFileSystem.Handle(descriptor);
        temporaryIdentity = LinuxFileSystem.Metadata(TemporaryHandle);
    }

    public override void Commit(AtomicFileWriteMode mode)
    {
        LinuxFileSystem.Statx? current = LinuxFileSystem.MetadataAt(Parent, TemporaryName);
        if (current is null || !temporaryIdentity.SameFile(current.Value) || current.Value.LinkCount != 1)
        {
            throw new IOException("The temporary file identity changed before promotion.");
        }

        LinuxFileSystem.Statx? destination = LinuxFileSystem.MetadataAt(Parent, destinationName);
        if (destination is not null && (destination.Value.Mode & 0xF000) != 0x8000)
        {
            throw new IOException("The destination must be absent or a regular file, never a link or special file.");
        }

        if (LinuxFileSystem.Rename(Parent, TemporaryName, Parent, destinationName,
                mode == AtomicFileWriteMode.CreateNew ? 1u : 0u) != 0)
        {
            throw LinuxFileSystem.Error("Promoting complete temporary file", Marshal.GetLastPInvokeError());
        }

        Committed = true;
    }

    public override void Dispose()
    {
        if (TemporaryHandle is not null && !Committed)
        {
            try
            {
                LinuxFileSystem.Statx? entry = LinuxFileSystem.MetadataAt(Parent, TemporaryName);
                if (entry is not null && temporaryIdentity.SameFile(entry.Value))
                {
                    // Best effort, anchored to the original directory, never recursive or by a substituted path.
                    LinuxFileSystem.Unlink(Parent, TemporaryName, 0);
                }
            }
            catch (IOException)
            {
                // Preserve the original write/cancellation failure. Recovery of leftovers is outside this component.
            }
        }

        TemporaryHandle?.Dispose();
        TemporaryHandle = null;
        for (int index = directories.Count - 1; index >= 0; index--) directories[index].Handle.Dispose();
        directories.Clear();
    }

    private bool OpenDirectory(string name, bool create, bool insideRoot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CheckPermissions(LinuxFileSystem.Metadata(Parent), directories[^1].InsideRoot);
        int descriptor = LinuxFileSystem.OpenAt(Parent, name, LinuxFileSystem.DirectoryFlags, 0);
        int error = Marshal.GetLastPInvokeError();
        if (descriptor < 0 && error == 2 && create)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (LinuxFileSystem.MakeDirectory(Parent, name, 0x1C0) != 0) // 0700, further restricted by umask
            {
                error = Marshal.GetLastPInvokeError();
                if (error != 17) throw LinuxFileSystem.Error("Creating generation directory", error);
            }

            descriptor = LinuxFileSystem.OpenAt(Parent, name, LinuxFileSystem.DirectoryFlags, 0);
            error = Marshal.GetLastPInvokeError();
        }

        if (descriptor < 0 && error == 2 && !create) return false;
        if (descriptor < 0) throw LinuxFileSystem.Error("Opening generation directory without following links", error);
        AddDirectory(name, LinuxFileSystem.Handle(descriptor), insideRoot);
        return true;
    }

    private void AddDirectory(string name, SafeFileHandle handle, bool insideRoot)
    {
        try
        {
            LinuxFileSystem.Statx identity = LinuxFileSystem.Metadata(handle);
            CheckPermissions(identity, insideRoot);
            if (insideRoot && rootMountId != 0 && identity.MountId != rootMountId)
            {
                throw new IOException("Mount-point traversal below the generation root is not allowed.");
            }

            directories.Add(new(name, handle, identity, insideRoot));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private void CheckPermissions(LinuxFileSystem.Statx metadata, bool insideRoot)
    {
        bool trustedOwner = metadata.UserId == 0 || metadata.UserId == effectiveUserId;
        bool writableByOthers = (metadata.Mode & 0x12) != 0;
        bool stickyAncestor = !insideRoot && (metadata.Mode & 0x200) != 0;
        if (!trustedOwner || (writableByOthers && !stickyAncestor))
        {
            throw new IOException("Generation directories must not be controlled by other users; only sticky shared ancestors are permitted.");
        }
    }

    private sealed record DirectoryAnchor(string Name, SafeFileHandle Handle, LinuxFileSystem.Statx Identity, bool InsideRoot);
}
