namespace ComputeQuiet;

public static class Defaults
{
    /// <summary>Windows services paused while Quiet is on (restored if they were running).</summary>
    public static readonly string[] QuietServices =
    [
        "SysMain",      // Superfetch / prefetch
        "WSearch",      // Windows Search indexer
        "DiagTrack",    // Telemetry
        "dmwappushservice",
        "RetailDemo",
        "Wisvc",        // Windows Insider
    ];

    /// <summary>Background updaters / sync clients suspended in balanced mode.</summary>
    public static readonly string[] KnownHogs =
    [
        "OneDrive",
        "OneDriveStandaloneUpdater",
        "MicrosoftEdgeUpdate",
        "GoogleUpdate",
        "GoogleCrashHandler",
        "GoogleCrashHandler64",
        "AdobeARM",
        "AdobeGCClient",
        "AGSService",
        "CCXProcess",
        "CoreSync",
        "Creative Cloud",
        "Dropbox",
        "DropboxUpdate",
        "Teams",
        "ms-teams",
        "Slack",
        "CCleaner",
        "YourPhone",
        "PhoneExperienceHost",
        "SearchIndexer",
        "SearchApp",
        "WidgetService",
        "Widgets",
        "SkypeApp",
        "SkypeBackgroundHost",
        "Copilot",
        "OfficeClickToRun",
        "AppVShNotify",
    ];

    /// <summary>Never suspend these — OS / shell stability.</summary>
    public static readonly string[] CriticalKeepAlive =
    [
        "Idle",
        "System",
        "Registry",
        "smss",
        "csrss",
        "wininit",
        "services",
        "lsass",
        "svchost",
        "winlogon",
        "fontdrvhost",
        "dwm",
        "explorer",
        "conhost",
        "sihost",
        "taskhostw",
        "RuntimeBroker",
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "SearchHost",
        "TextInputHost",
        "SecurityHealthSystray",
        "SecurityHealthService",
        "MsMpEng",
        "NisSrv",
        "Memory Compression",
        "audiodg",
        "WmiPrvSE",
        "dllhost",
        "ctfmon",
        "CompPkgSrv",
        "ApplicationFrameHost",
        "SystemSettings",
        "UserOOBEBroker",
        "backgroundTaskHost",
        "LockApp",
        "LogonUI",
        "ComputeQuiet",
    ];
}
