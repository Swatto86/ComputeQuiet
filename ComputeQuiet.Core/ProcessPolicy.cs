namespace ComputeQuiet;

public static class ProcessPolicy
{
    public static bool IsCritical(string processName) =>
        Defaults.CriticalKeepAlive.Contains(Normalize(processName), StringComparer.OrdinalIgnoreCase);

    public static bool IsKnownHog(string processName)
    {
        var name = Normalize(processName);
        return Defaults.KnownHogs.Any(h =>
            string.Equals(Normalize(h), name, StringComparison.OrdinalIgnoreCase));
    }

    public static bool ShouldKeepAlive(string processName, QuietOptions options)
    {
        var name = Normalize(processName);
        if (IsCritical(name))
            return true;
        if (options.ExtraKeepAlive.Contains(name))
            return true;
        return false;
    }

    public static bool ShouldSuspend(string processName, QuietOptions options)
    {
        if (ShouldKeepAlive(processName, options))
            return false;

        if (options.Aggressive)
            return true;

        return IsKnownHog(processName);
    }

    public static string Normalize(string processName)
    {
        var name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name;
    }
}
