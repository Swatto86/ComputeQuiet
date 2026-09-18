namespace ComputeQuiet;

public sealed class QuietOptions
{
    /// <summary>
    /// When true, also suspend session processes that have no visible window
    /// (interactive apps with a UI stay running). When false, only known background hogs.
    /// </summary>
    public bool Aggressive { get; set; }

    /// <summary>Extra process names (without .exe) the user wants to keep running.</summary>
    public HashSet<string> ExtraKeepAlive { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
