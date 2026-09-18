using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ServiceProcess;

namespace ComputeQuiet;

[SupportedOSPlatform("windows")]
public sealed class QuietEngine
{
    const uint PROCESS_SUSPEND_RESUME = 0x0800;
    const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    readonly List<string> _log = [];

    public IReadOnlyList<string> Log => _log;

    public QuietState Enable(QuietOptions options)
    {
        _log.Clear();
        var previous = StateStore.Load();
        if (previous.IsQuiet)
        {
            LogLine("Already quiet — restoring first, then re-applying.");
            Disable();
        }

        var state = new QuietState
        {
            IsQuiet = true,
            QuietedAt = DateTimeOffset.Now,
            Aggressive = options.Aggressive,
        };

        var settings = AppSettings.Load();
        if (settings.SwitchPowerPlan)
            state.PreviousPowerSchemeGuid = PowerPlanSwitcher.SwitchToHighPerformance(LogLine);

        StopServices(state);
        SuspendProcesses(options, state);

        state.Log = [.. _log];
        StateStore.Save(state);
        LogLine($"Quiet on — suspended {state.SuspendedPids.Count} process(es), stopped {state.StoppedServices.Count} service(s).");
        return state;
    }

    public QuietState Disable()
    {
        _log.Clear();
        var state = StateStore.Load();
        if (!state.IsQuiet && state.SuspendedPids.Count == 0 && state.StoppedServices.Count == 0 &&
            string.IsNullOrEmpty(state.PreviousPowerSchemeGuid))
        {
            LogLine("Nothing to restore.");
            return state;
        }

        ResumeProcesses(state);
        StartServices(state);
        PowerPlanSwitcher.Restore(state.PreviousPowerSchemeGuid, LogLine);

        state.IsQuiet = false;
        state.QuietedAt = null;
        state.SuspendedPids.Clear();
        state.StoppedServices.Clear();
        state.PreviousPowerSchemeGuid = null;
        state.Log = [.. _log];
        StateStore.Save(state);
        LogLine("Restored.");
        return state;
    }

    void StopServices(QuietState state)
    {
        foreach (var name in Defaults.QuietServices)
        {
            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(8));
                    state.StoppedServices.Add(name);
                    LogLine($"Stopped service {name}");
                }
            }
            catch (Exception ex)
            {
                LogLine($"Service {name}: {ex.Message}");
            }
        }
    }

    void StartServices(QuietState state)
    {
        foreach (var name in state.StoppedServices.ToArray())
        {
            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(12));
                    LogLine($"Started service {name}");
                }
            }
            catch (Exception ex)
            {
                LogLine($"Service {name} restart: {ex.Message}");
            }
        }
    }

    void SuspendProcesses(QuietOptions options, QuietState state)
    {
        var self = Environment.ProcessId;
        var foregroundPid = GetForegroundProcessId();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == self || process.Id == 0 || process.Id == foregroundPid)
                    continue;

                string name;
                try { name = process.ProcessName; }
                catch { continue; }

                if (!ProcessPolicy.ShouldSuspend(name, options))
                    continue;

                // Only touch processes in our session (skip Session 0 services).
                int sessionId;
                try { sessionId = process.SessionId; }
                catch { continue; }
                if (sessionId == 0)
                    continue;

                if (TrySuspend(process.Id))
                {
                    state.SuspendedPids.Add(process.Id);
                    LogLine($"Suspended {name} ({process.Id})");
                }
            }
            catch
            {
                // Access denied / exited — skip.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    static int GetForegroundProcessId()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return -1;
        _ = GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    void ResumeProcesses(QuietState state)
    {
        foreach (var pid in state.SuspendedPids.ToArray())
        {
            try
            {
                if (TryResume(pid))
                    LogLine($"Resumed PID {pid}");
                else
                    LogLine($"PID {pid} already gone");
            }
            catch (Exception ex)
            {
                LogLine($"Resume {pid}: {ex.Message}");
            }
        }
    }

    static bool TrySuspend(int pid)
    {
        var handle = OpenProcess(PROCESS_SUSPEND_RESUME | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero)
            return false;
        try
        {
            return NtSuspendProcess(handle) == 0;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    static bool TryResume(int pid)
    {
        try { _ = Process.GetProcessById(pid); }
        catch (ArgumentException) { return false; }

        var handle = OpenProcess(PROCESS_SUSPEND_RESUME | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero)
            return false;
        try
        {
            return NtResumeProcess(handle) == 0;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    void LogLine(string message) => _log.Add($"{DateTime.Now:HH:mm:ss}  {message}");

    [DllImport("ntdll.dll")]
    static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll")]
    static extern int NtResumeProcess(IntPtr processHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
