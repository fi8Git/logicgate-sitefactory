using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

internal static partial class DirectoryLinkFixture
{
    internal static void Create(string path, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            CreateJunction(path, target);
        }
        else if (OperatingSystem.IsLinux())
        {
            Directory.CreateSymbolicLink(path, target);
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
    }

    [SupportedOSPlatform("windows")]
    private static void CreateJunction(string path, string target)
    {
        Directory.CreateDirectory(path);
        using SafeFileHandle handle = OpenDirectory(path, 0x40000000, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) throw new IOException($"Junction fixture open failed: {Marshal.GetLastPInvokeError()}.");
        byte[] substitute = Encoding.Unicode.GetBytes("\\??\\" + Path.GetFullPath(target));
        byte[] printable = Encoding.Unicode.GetBytes(Path.GetFullPath(target));
        byte[] buffer = new byte[16 + substitute.Length + 2 + printable.Length + 2];
        BitConverter.GetBytes(0xA0000003u).CopyTo(buffer, 0); // IO_REPARSE_TAG_MOUNT_POINT.
        BitConverter.GetBytes(checked((ushort)(buffer.Length - 8))).CopyTo(buffer, 4);
        BitConverter.GetBytes(checked((ushort)substitute.Length)).CopyTo(buffer, 10);
        BitConverter.GetBytes(checked((ushort)(substitute.Length + 2))).CopyTo(buffer, 12);
        BitConverter.GetBytes(checked((ushort)printable.Length)).CopyTo(buffer, 14);
        substitute.CopyTo(buffer, 16);
        printable.CopyTo(buffer, 16 + substitute.Length + 2);
        IntPtr data = Marshal.AllocHGlobal(buffer.Length);
        try
        {
            Marshal.Copy(buffer, 0, data, buffer.Length);
            if (!SetReparsePoint(handle, 0x000900A4, data, (uint)buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
            {
                throw new IOException($"Junction fixture creation failed: {Marshal.GetLastPInvokeError()}.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle OpenDirectory(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [LibraryImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetReparsePoint(SafeFileHandle handle, uint control, IntPtr input, uint inputSize, IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);
}
