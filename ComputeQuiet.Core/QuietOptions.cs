namespace ComputeQuiet;

public sealed class QuietOptions
{
    /// <summary>
    /// When true, suspend every non-whitelisted process in the current user session.
    /// When false, only suspend known background hogs plus stop quiet services.
    /// </summary>
    public bool Aggressive { get; set; }

    /// <summary>Extra process names (without .exe) the user wants to keep running.</summary>
    public HashSet<string> ExtraKeepAlive { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
