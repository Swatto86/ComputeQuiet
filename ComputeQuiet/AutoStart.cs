using Microsoft.Win32;
using System.Runtime.Versioning;
using IOPath = System.IO.Path;

namespace ComputeQuiet;

[SupportedOSPlatform("windows")]
public static class AutoStart
{
    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "ComputeQuiet";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exe = Environment.ProcessPath
            ?? IOPath.Combine(AppContext.BaseDirectory, "ComputeQuiet.exe");
        key.SetValue(ValueName, $"\"{exe}\" --minimized");
    }
}
