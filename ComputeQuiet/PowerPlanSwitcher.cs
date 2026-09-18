using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace ComputeQuiet;

[SupportedOSPlatform("windows")]
public static class PowerPlanSwitcher
{
    // Well-known Windows high-performance scheme.
    public const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public static string? GetActiveSchemeGuid()
    {
        var output = RunPowerCfg("/getactivescheme");
        if (output is null) return null;
        var match = Regex.Match(output, @"GUID:\s*([0-9a-fA-F\-]{36})");
        return match.Success ? match.Groups[1].Value : null;
    }

    public static bool TrySetActiveScheme(string guid, out string message)
    {
        var output = RunPowerCfg($"/setactive {guid}");
        if (output is null)
        {
            message = "powercfg failed";
            return false;
        }

        if (output.Contains("Invalid", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            message = output.Trim();
            return false;
        }

        message = $"Active scheme set to {guid}";
        return true;
    }

    /// <summary>
    /// Switch to High Performance (or keep current if already there).
    /// Returns the previous scheme GUID so it can be restored.
    /// </summary>
    public static string? SwitchToHighPerformance(Action<string> log)
    {
        var previous = GetActiveSchemeGuid();
        if (previous is null)
        {
            log("Could not read active power scheme.");
            return null;
        }

        if (string.Equals(previous, HighPerformanceGuid, StringComparison.OrdinalIgnoreCase))
        {
            log("Already on High Performance.");
            return previous;
        }

        if (TrySetActiveScheme(HighPerformanceGuid, out var message))
            log($"Power plan → High Performance (was {previous}).");
        else
            log($"Power plan switch skipped: {message}");

        return previous;
    }

    public static void Restore(string? previousGuid, Action<string> log)
    {
        if (string.IsNullOrWhiteSpace(previousGuid))
            return;

        var current = GetActiveSchemeGuid();
        if (string.Equals(current, previousGuid, StringComparison.OrdinalIgnoreCase))
        {
            log("Power plan already restored.");
            return;
        }

        if (TrySetActiveScheme(previousGuid, out _))
            log($"Power plan restored ({previousGuid}).");
        else
            log($"Could not restore power plan {previousGuid}.");
    }

    static string? RunPowerCfg(string args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (process is null) return null;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(8000);
            return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
        }
        catch
        {
            return null;
        }
    }
}
