using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LogicGate.SiteFactory.Cli.Tests;

// Test-only OS boundaries, established before the command can spawn children.
internal static class OwnedProcessScope
{
    internal static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(10);

    internal static SafeFileHandle CreateJob(string name)
    {
        SafeFileHandle job = CreateJobObject(IntPtr.Zero, name);
        if (job.IsInvalid)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        JobLimits limits = new() { Basic = new() { LimitFlags = 0x2000 } }; // KILL_ON_JOB_CLOSE; no breakaway.
        if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<JobLimits>()))
        {
            job.Dispose();
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        return job;
    }

    internal static void Enter(string jobName)
    {
        if (OperatingSystem.IsWindows())
        {
            // Only the caller retains a job handle: its death also closes the entire job.
            using SafeFileHandle job = OpenJobObject(1, false, jobName);
            using Process current = Process.GetCurrentProcess();
            if (job.IsInvalid || !AssignProcessToJobObject(job, current.Handle))
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        else if (OperatingSystem.IsLinux())
        {
            // Orphans are adopted by this private supervisor, never by the shared testhost.
            if (setsid() < 0 || prctl(36, 1, 0, 0, 0) != 0) // PR_SET_CHILD_SUBREAPER
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        else
            throw new PlatformNotSupportedException("Owned test processes require Windows or Linux.");
    }

    internal static async Task TerminateJobAsync(SafeFileHandle job)
    {
        if (!TerminateJobObject(job, 137))
            throw new InvalidOperationException("Owned test job termination failed.");
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < CleanupTimeout)
        {
            if (!QueryInformationJobObject(job, 1, out JobAccounting accounting, (uint)Marshal.SizeOf<JobAccounting>(), IntPtr.Zero))
                throw new InvalidOperationException("Owned test job accounting failed.");
            if (accounting.ActiveProcesses == 0)
                return;
            await Task.Delay(20);
        }
        throw new TimeoutException("Owned test job still has active processes after cleanup.");
    }

    internal static async Task ReapLinuxChildrenAsync(Process command)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        Task commandExit = command.WaitForExitAsync();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            int[] children = ReadChildren();
            if (children.Length == 0 && commandExit.IsCompleted)
            {
                await commandExit;
                // Adoption can race a /proc thread-by-thread snapshot. Only the kernel's ECHILD
                // proves that no child (running or zombie) remains, and therefore no child can fork.
                int remaining = waitpid(-1, out _, 1);
                if (remaining == -1 && Marshal.GetLastPInvokeError() == 10) // ECHILD
                    return;
            }
            foreach (int pid in children)
            {
                // A pidfd pins identity; a PID recycled outside this invocation cannot be signalled.
                using SafeFileHandle child = new((IntPtr)pidfd_open(pid, 0), ownsHandle: true);
                if (!child.IsInvalid && ReadChildren().Contains(pid)
                    && pidfd_send_signal(child, 9, IntPtr.Zero, 0) != 0 && Marshal.GetLastPInvokeError() != 3)
                    throw new InvalidOperationException("Owned test descendant termination failed.");
                // .NET reaps the direct Process. Only reap adopted descendants ourselves.
                if (pid != command.Id)
                    _ = waitpid(pid, out _, 1); // WNOHANG
            }
            await Task.Delay(20);
        }
        throw new TimeoutException("Owned test descendants did not terminate within the cleanup deadline.");
    }

    internal static void AbortLinuxScope()
    {
        // Emergency fallback only, while this private session's leader is still alive.
        // Its group ID cannot be reused by an unrelated process. Never select by executable name.
        _ = kill(-Environment.ProcessId, 9);
    }

    private static int[] ReadChildren() => Directory.EnumerateDirectories("/proc/self/task")
        .SelectMany(static task => ReadThreadChildren(task)).Distinct().ToArray();

    private static int[] ReadThreadChildren(string task)
    {
        try
        {
            return File.ReadAllText(Path.Combine(task, "children")).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(static value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        }
        catch (DirectoryNotFoundException) { return []; } // Runtime thread exited during the snapshot.
        catch (FileNotFoundException) { return []; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobBasicLimits
    {
        public long ProcessTime, JobTime;
        public uint LimitFlags;
        public nuint MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobLimits
    {
        public JobBasicLimits Basic;
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
        public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobAccounting
    {
        public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime;
        public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle OpenJobObject(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref JobLimits limits, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(SafeFileHandle job, int informationClass, out JobAccounting accounting, uint size, IntPtr returnedLength);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
    [DllImport("libc", SetLastError = true)]
    private static extern int setsid();
    [DllImport("libc", SetLastError = true)]
    private static extern int prctl(int option, nuint arg2, nuint arg3, nuint arg4, nuint arg5);
    [DllImport("libc", SetLastError = true)]
    private static extern int pidfd_open(int pid, uint flags);
    [DllImport("libc", SetLastError = true)]
    private static extern int pidfd_send_signal(SafeFileHandle pidfd, int signal, IntPtr info, uint flags);
    [DllImport("libc", SetLastError = true)]
    private static extern int waitpid(int pid, out int status, int options);
    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int signal);
}
