using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

[SupportedOSPlatform("windows")]
internal static partial class WindowsFileSystem
{
    internal const int NameNotFound = unchecked((int)0xC0000034);

    internal static SafeFileHandle OpenVolume(string volume)
    {
        if (GetDriveType(volume) != 3) throw new IOException("Atomic generation requires a local fixed Windows volume.");
        SafeFileHandle handle = CreateFile(volume, 0x100081, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw Error("Opening generation volume", error);
        }

        try
        {
            RejectReparsePoint(handle);
            IntPtr name = Marshal.AllocHGlobal(64);
            try
            {
                if (!GetVolumeInformation(handle, IntPtr.Zero, 0, out _, out _, out _, name, 32))
                {
                    throw Error("Reading volume capabilities", Marshal.GetLastPInvokeError());
                }

                string? fileSystem = Marshal.PtrToStringUni(name);
                if (fileSystem is not ("NTFS" or "ReFS"))
                {
                    throw new IOException("Atomic generation supports NTFS and ReFS Windows volumes only.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(name);
            }

            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static unsafe int OpenChild(SafeFileHandle parent, string name, bool directory, bool create,
        bool temporary, IntPtr securityDescriptor, out SafeFileHandle handle, bool readContent = false)
    {
        if (name.Length > 32766) throw new IOException("A filesystem component is too long.");
        bool referenceAdded = false;
        try
        {
            parent.DangerousAddRef(ref referenceAdded);
            fixed (char* characters = name)
            {
                UnicodeString unicode = new()
                {
                    Length = (ushort)(name.Length * 2),
                    MaximumLength = (ushort)(name.Length * 2),
                    Buffer = (IntPtr)characters,
                };
                ObjectAttributes attributes = new()
                {
                    Length = (uint)Marshal.SizeOf<ObjectAttributes>(),
                    RootDirectory = parent.DangerousGetHandle(),
                    ObjectName = (IntPtr)(&unicode),
                    Attributes = 0x40, // OBJ_CASE_INSENSITIVE, matching the Windows logical confinement contract.
                    SecurityDescriptor = securityDescriptor,
                };
                // LIST_DIRECTORY is necessary for delete-sharing restrictions; attribute-only handles do not pin names.
                uint access = 0x100080u | (directory || readContent ? 1u : 0u) | (temporary ? 0x10002u : 0u);
                uint options = 0x200020u | (directory ? 1u : 0x40u); // OPEN_REPARSE_POINT, SYNCHRONOUS_IO_NONALERT, DIRECTORY/NON_DIRECTORY.
                return CreateRelative(out handle, access, in attributes, out _, IntPtr.Zero,
                    directory ? 0x10u : 0x80u,
                    temporary ? 0u : directory ? 3u : 7u,
                    temporary ? 2u : create ? 3u : 1u, options, IntPtr.Zero, 0);
            }
        }
        finally
        {
            if (referenceAdded) parent.DangerousRelease();
        }
    }

    internal static void RejectReparsePoint(SafeFileHandle handle)
    {
        if (!GetAttributeTag(handle, 9, out AttributeTag information, 8))
        {
            throw Error("Inspecting anchored file attributes", Marshal.GetLastPInvokeError());
        }

        if ((information.Attributes & 0x400) != 0)
        {
            throw new IOException("Reparse points, including symlinks and junctions, are not allowed in a generation path.");
        }
    }

    internal static string FileIdentity(SafeFileHandle handle)
    {
        if (!GetFileId(handle, 18, out FileIdInformation information, 24))
        {
            throw Error("Inspecting destination identity", Marshal.GetLastPInvokeError());
        }

        return FormattableString.Invariant($"{information.Volume:X16}:{information.IdLow:X16}:{information.IdHigh:X16}");
    }

    internal static void Rename(SafeFileHandle file, SafeFileHandle parent, string name, bool replace)
    {
        byte[] nameBytes = Encoding.Unicode.GetBytes(name);
        int parentOffset = IntPtr.Size;
        int lengthOffset = parentOffset + IntPtr.Size;
        int nameOffset = lengthOffset + sizeof(uint);
        // FILE_RENAME_INFORMATION includes WCHAR[1] plus tail padding even for a one-character name.
        int bufferLength = Math.Max(nameOffset + nameBytes.Length, nameOffset + sizeof(uint));
        IntPtr buffer = Marshal.AllocHGlobal(bufferLength);
        bool referenceAdded = false;
        try
        {
            parent.DangerousAddRef(ref referenceAdded);
            for (int index = 0; index < bufferLength; index++) Marshal.WriteByte(buffer, index, 0);
            // REPLACE_IF_EXISTS | POSIX_SEMANTICS: existing readers retain their complete old file.
            Marshal.WriteInt32(buffer, 0, replace ? 3 : 0);
            Marshal.WriteIntPtr(buffer, parentOffset, parent.DangerousGetHandle());
            Marshal.WriteInt32(buffer, lengthOffset, nameBytes.Length);
            Marshal.Copy(nameBytes, 0, buffer + nameOffset, nameBytes.Length);
            Check(SetInformation(file, out _, buffer, (uint)bufferLength, 65), "Promoting complete temporary file");
        }
        finally
        {
            if (referenceAdded) parent.DangerousRelease();
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static unsafe void DeleteOnClose(SafeFileHandle file)
    {
        byte delete = 1;
        Check(SetInformation(file, out _, (IntPtr)(&delete), 1, 13), "Removing owned temporary file");
    }

    internal static IntPtr PrivateSecurityDescriptor()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ?? throw new IOException("The current Windows user SID is unavailable.");
        if (!ConvertSecurityDescriptor($"D:P(A;OICI;FA;;;{sid})", 1, out IntPtr descriptor, out _))
        {
            throw Error("Preparing private generation permissions", Marshal.GetLastPInvokeError());
        }

        return descriptor;
    }

    internal static void Check(int status, string operation)
    {
        if (status < 0) throw new IOException($"{operation} failed (Windows status 0x{status:X8}).");
    }

    private static IOException Error(string operation, int code) => new($"{operation} failed (Windows error {code}).");

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        internal ushort Length;
        internal ushort MaximumLength;
        internal IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        internal uint Length;
        internal IntPtr RootDirectory;
        internal IntPtr ObjectName;
        internal uint Attributes;
        internal IntPtr SecurityDescriptor;
        internal IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        internal IntPtr Status;
        internal UIntPtr Information;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeTag
    {
        internal uint Attributes;
        internal uint ReparseTag;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInformation
    {
        internal ulong Volume;
        internal ulong IdLow;
        internal ulong IdHigh;
    }

    [LibraryImport("ntdll.dll", EntryPoint = "NtCreateFile")]
    private static partial int CreateRelative(out SafeFileHandle handle, uint access, in ObjectAttributes attributes,
        out IoStatusBlock status, IntPtr allocationSize, uint fileAttributes, uint share, uint disposition, uint options, IntPtr extendedAttributes, uint extendedAttributesLength);

    [LibraryImport("ntdll.dll", EntryPoint = "NtSetInformationFile")]
    private static partial int SetInformation(SafeFileHandle handle, out IoStatusBlock status, IntPtr information, uint length, uint informationClass);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr securityAttributes, uint disposition, uint options, IntPtr template);

    [LibraryImport("kernel32.dll", EntryPoint = "GetDriveTypeW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint GetDriveType(string rootPath);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetAttributeTag(SafeFileHandle handle, int informationClass, out AttributeTag information, uint size);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileId(SafeFileHandle handle, int informationClass, out FileIdInformation information, uint size);

    [LibraryImport("kernel32.dll", EntryPoint = "GetVolumeInformationByHandleW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetVolumeInformation(SafeFileHandle handle, IntPtr volumeName, uint volumeNameSize,
        out uint serial, out uint maximumComponent, out uint flags, IntPtr fileSystemName, uint fileSystemNameSize);

    [LibraryImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ConvertSecurityDescriptor(string text, uint revision, out IntPtr descriptor, out uint size);

    [LibraryImport("kernel32.dll", EntryPoint = "LocalFree")]
    internal static partial IntPtr FreeSecurityDescriptor(IntPtr descriptor);
}
