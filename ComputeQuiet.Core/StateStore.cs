using System.Text.Json;

namespace ComputeQuiet;

public static class StateStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string StateDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ComputeQuiet");

    public static string StatePath => Path.Combine(StateDirectory, "state.json");

    public static QuietState Load()
    {
        try
        {
            if (!File.Exists(StatePath))
                return new QuietState();
            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<QuietState>(json, JsonOptions) ?? new QuietState();
        }
        catch
        {
            return new QuietState();
        }
    }

    public static void Save(QuietState state)
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, JsonOptions));
    }

    public static void Clear()
    {
        if (File.Exists(StatePath))
            File.Delete(StatePath);
    }
}
