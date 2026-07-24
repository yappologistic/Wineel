using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;

namespace Wineel;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public SettingsStore(string path) => Path = path;
    public string Path { get; }
    public string? LastRecoveryBackup { get; private set; }

    public AppSettings Load()
    {
        if (!File.Exists(Path)) return new AppSettings();
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(Path))?.AsObject()
                       ?? throw new JsonException("Settings root is empty.");
            root = Migrate(root);
            return root.Deserialize<AppSettings>(Options) ?? new AppSettings();
        }
        catch (Exception exception) when (exception is JsonException
                                             or InvalidOperationException
                                             or FormatException
                                             or OverflowException
                                             or IOException
                                             or UnauthorizedAccessException)
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
            LastRecoveryBackup = $"{Path}.corrupt-{timestamp}.json";
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.Move(Path, LastRecoveryBackup, true);
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = System.IO.Path.GetDirectoryName(Path) ?? throw new InvalidOperationException("Settings path has no directory.");
        Directory.CreateDirectory(directory);
        var temp = $"{Path}.{Environment.ProcessId}.tmp";
        var json = JsonSerializer.Serialize(settings with { Version = AppSettings.CurrentVersion }, Options);
        File.WriteAllText(temp, json);
        if (File.Exists(Path)) File.Replace(temp, Path, null);
        else File.Move(temp, Path);
    }

    public static JsonObject Migrate(JsonObject root)
    {
        var version = root["Version"]?.GetValue<int>() ?? root["version"]?.GetValue<int>() ?? 1;
        if (version <= 1)
        {
            var legacy = root["EnableAltTab"] ?? root["enableAltTab"];
            if (legacy is not null && root["ReplaceAltTab"] is null) root["ReplaceAltTab"] = legacy.DeepClone();
            root.Remove("EnableAltTab");
            root.Remove("enableAltTab");
            root["Version"] = 2;
        }
        if (version <= 2)
        {
            var priorWheelSize = ReadNumber(root, "WheelSize", "wheelSize", 500);
            if (Math.Abs(priorWheelSize - 500) < 0.001) root["WheelSize"] = 440;
            root["Version"] = 3;
        }
        if (version <= 3)
        {
            var priorWheelSize = ReadNumber(root, "WheelSize", "wheelSize", 440);
            var priorPlateOpacity = ReadNumber(root, "PlateOpacity", "plateOpacity", 0.72);
            if (Math.Abs(priorWheelSize - 440) < 0.001) root["WheelSize"] = 400;
            if (Math.Abs(priorPlateOpacity - 0.72) < 0.001) root["PlateOpacity"] = 0.68;
            root["Version"] = AppSettings.CurrentVersion;
        }
        if (version <= 4)
        {
            root["PinnedIdentities"] ??= new JsonArray();
            root["Version"] = AppSettings.CurrentVersion;
        }
        return root;
    }

    private static double ReadNumber(JsonObject root, string name, string legacyName, double fallback)
    {
        var node = root[name] ?? root[legacyName];
        return node is not null && double.TryParse(node.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
