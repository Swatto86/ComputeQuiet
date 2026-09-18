namespace ComputeQuiet;

public sealed class QuietState
{
    public bool IsQuiet { get; set; }
    public DateTimeOffset? QuietedAt { get; set; }
    public bool Aggressive { get; set; }
    public List<int> SuspendedPids { get; set; } = [];
    public List<string> StoppedServices { get; set; } = [];
    /// <summary>Power scheme GUID active before Quiet switched plans.</summary>
    public string? PreviousPowerSchemeGuid { get; set; }
    public List<string> Log { get; set; } = [];
}
