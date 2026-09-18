namespace ComputeQuiet;

public static class SelfCheck
{
    public static bool Run()
    {
        var failures = 0;

        void Assert(bool condition, string message)
        {
            if (condition) return;
            Console.Error.WriteLine("FAIL: " + message);
            failures++;
        }

        Assert(ProcessPolicy.IsCritical("explorer"), "explorer is critical");
        Assert(ProcessPolicy.IsCritical("Explorer.EXE"), "explorer.exe normalizes");
        Assert(!ProcessPolicy.IsCritical("chrome"), "chrome is not critical");

        var balanced = new QuietOptions { Aggressive = false };
        Assert(ProcessPolicy.ShouldSuspend("OneDrive", balanced), "OneDrive suspended in balanced");
        Assert(!ProcessPolicy.ShouldSuspend("Steam", balanced), "Steam kept in balanced");
        Assert(!ProcessPolicy.ShouldSuspend("chrome", balanced), "chrome kept in balanced");

        var aggressive = new QuietOptions { Aggressive = true };
        Assert(ProcessPolicy.ShouldSuspend("chrome", aggressive), "chrome suspended when aggressive");
        Assert(!ProcessPolicy.ShouldSuspend("dwm", aggressive), "dwm never suspended");

        aggressive.ExtraKeepAlive.Add("chrome");
        Assert(!ProcessPolicy.ShouldSuspend("chrome", aggressive), "ExtraKeepAlive honored");

        var state = new QuietState
        {
            IsQuiet = true,
            SuspendedPids = [1, 2, 3],
            StoppedServices = ["SysMain"],
            PreviousPowerSchemeGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
        };
        var dir = Path.Combine(Path.GetTempPath(), "ComputeQuietSelfCheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(state);
            var roundTrip = System.Text.Json.JsonSerializer.Deserialize<QuietState>(json);
            Assert(roundTrip is { IsQuiet: true, SuspendedPids.Count: 3 }, "state round-trip");
            Assert(roundTrip!.PreviousPowerSchemeGuid is not null, "power scheme persisted");

            var settings = new AppSettings { StartWithWindows = true, SwitchPowerPlan = false };
            var settingsJson = System.Text.Json.JsonSerializer.Serialize(settings);
            var settingsRoundTrip = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(settingsJson);
            Assert(settingsRoundTrip is { StartWithWindows: true, SwitchPowerPlan: false }, "settings round-trip");
        }
        finally
        {
            Directory.Delete(dir, true);
        }

        if (failures == 0)
            Console.WriteLine("ComputeQuiet self-check OK");
        return failures == 0;
    }
}
