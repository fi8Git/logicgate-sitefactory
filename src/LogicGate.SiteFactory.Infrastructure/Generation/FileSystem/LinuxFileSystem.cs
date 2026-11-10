using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

[SupportedOSPlatform("linux")]
internal static partial class LinuxFileSystem
{
    internal const int CloseOnExec = 0x80000;
    internal const int PathOnly = 0x200000;
    internal const uint MetadataMask = 0x110F; // TYPE, MODE, NLINK, UID, INO, MNT_ID (Linux >= 5.8).

    internal static int NoFollow => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => 0x20000,
        Architecture.Arm64 => 0x8000,
        _ => throw new PlatformNotSupportedException("Linux atomic writes require x64 or arm64."),
    };

    internal static int DirectoryFlags => PathOnly | CloseOnExec | NoFollow
        | (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? 0x4000 : 0x10000);

    internal static SafeFileHandle Handle(int descriptor) => new((IntPtr)descriptor, ownsHandle: true);

    internal static IOException Error(string operation, int error) => new($"{operation} failed (Linux error {error}).");

    internal static Statx Metadata(SafeFileHandle handle)
    {
        if (Stat(handle, "", 0x1000, MetadataMask, out Statx value) != 0)
        {
            throw Error("Reading anchored metadata", Marshal.GetLastPInvokeError());
        }

        RequireMetadata(value);
        return value;
    }

    internal static Statx? MetadataAt(SafeFileHandle parent, string name)
    {
        if (Stat(parent, name, 0x100, MetadataMask, out Statx value) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            if (error == 2) return null; // ENOENT only; permission and other failures must not mean "absent".
            throw Error("Reading anchored entry metadata", error);
        }

        RequireMetadata(value);
        return value;
    }

    internal static void RequireSupportedFileSystem(SafeFileHandle directory)
    {
        IntPtr buffer = Marshal.AllocHGlobal(256);
        try
        {
            if (StatFileSystem(directory, buffer) != 0)
            {
                throw Error("Reading filesystem capabilities", Marshal.GetLastPInvokeError());
            }

            // Local filesystems with same-directory atomic rename; no network/FUSE or copy fallback.
            uint type = unchecked((uint)Marshal.ReadInt64(buffer));
            if (type is not (0xEF53 or 0x58465342 or 0x9123683E or 0x01021994 or 0x794C7630))
            {
                throw new IOException("The generation filesystem is not supported for atomic writes.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void RequireMetadata(Statx value)
    {
        if ((value.Mask & MetadataMask) != MetadataMask)
        {
            throw new IOException("Required filesystem identity and permission metadata is unavailable.");
        }
    }

    // Stable Linux UAPI layout, independent of the libc struct stat ABI. Only needed fields are exposed.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    internal struct Statx
    {
        [FieldOffset(0)] internal uint Mask;
        [FieldOffset(16)] internal uint LinkCount;
        [FieldOffset(20)] internal uint UserId;
        [FieldOffset(28)] internal ushort Mode;
        [FieldOffset(32)] internal ulong Inode;
        [FieldOffset(136)] internal uint DeviceMajor;
        [FieldOffset(140)] internal uint DeviceMinor;
        [FieldOffset(144)] internal ulong MountId;

        internal readonly bool SameFile(Statx other) => Inode == other.Inode && MountId == other.MountId
            && DeviceMajor == other.DeviceMajor && DeviceMinor == other.DeviceMinor;
    }

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Open(string path, int flags, uint mode);

    [LibraryImport("libc", EntryPoint = "openat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int OpenAt(SafeFileHandle parent, string name, int flags, uint mode);

    [LibraryImport("libc", EntryPoint = "mkdirat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int MakeDirectory(SafeFileHandle parent, string name, uint mode);

    [LibraryImport("libc", EntryPoint = "renameat2", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Rename(SafeFileHandle oldParent, string oldName, SafeFileHandle newParent, string newName, uint flags);

    [LibraryImport("libc", EntryPoint = "unlinkat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Unlink(SafeFileHandle parent, string name, int flags);

    [LibraryImport("libc", EntryPoint = "statx", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Stat(SafeFileHandle parent, string name, int flags, uint mask, out Statx value);

    [LibraryImport("libc", EntryPoint = "fstatfs", SetLastError = true)]
    private static partial int StatFileSystem(SafeFileHandle descriptor, IntPtr buffer);

    [LibraryImport("libc", EntryPoint = "geteuid")]
    internal static partial uint EffectiveUserId();
}
