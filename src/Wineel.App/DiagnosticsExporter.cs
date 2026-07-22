using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace Wineel;

internal static class DiagnosticsExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Export(string destination, string dataRoot, AppSettings settings)
    {
        var parent = Path.GetDirectoryName(destination) ?? throw new InvalidOperationException("The diagnostics destination has no parent directory.");
        Directory.CreateDirectory(parent);
        if (File.Exists(destination)) File.Delete(destination);

        using var archive = ZipFile.Open(destination, ZipArchiveMode.Create);
        var snapshot = new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            privacy = "No window titles, raw keystrokes, executable paths, user name, or machine name are included.",
            application = new { name = "Wineel", version = ApplicationInfo.Version },
            runtime = new
            {
                operatingSystem = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription,
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                highContrast = SystemParameters.HighContrast,
            },
            settings = new
            {
                settings.Version,
                settings.ReplaceAltTab,
                settings.StartWithWindows,
                settings.LaunchMinimized,
                settings.IsPaused,
                groupingMode = settings.GroupingMode.ToString(),
                settings.CurrentVirtualDesktopOnly,
                settings.DisableInExclusiveFullscreen,
                settings.WheelSize,
                settings.IconSize,
                settings.MaximumVisibleIcons,
                settings.PlateOpacity,
                settings.BeamIntensity,
                settings.AnimationSpeed,
                settings.ShowLabels,
                settings.ShowNumberBadges,
                themeMode = settings.ThemeMode.ToString(),
                motionPreference = settings.MotionPreference.ToString(),
                wheelAnchor = settings.WheelAnchor.ToString(),
                settings.ReverseWheel,
                settings.RepeatTabEnabled,
                settings.MouseClickSelection,
                settings.WrapSelection,
                pinnedApplicationCount = settings.PinnedIdentities.Count,
                exclusionCount = settings.Exclusions.Count,
            },
        };

        WriteEntry(archive, "diagnostics.json", JsonSerializer.Serialize(snapshot, JsonOptions));
        var logText = ReadSanitizedLogs(Path.Combine(dataRoot, "Logs"));
        if (!string.IsNullOrWhiteSpace(logText)) WriteEntry(archive, "recent-events.log", logText);
        WriteEntry(archive, "README.txt", "Wineel diagnostics export\r\n\r\nReview every file before sharing it. This archive deliberately omits window titles, raw keystrokes, executable paths, user and machine names, pinned identities, and exclusion values.\r\n");
    }

    private static string ReadSanitizedLogs(string logDirectory)
    {
        if (!Directory.Exists(logDirectory)) return string.Empty;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var lines = Directory.EnumerateFiles(logDirectory, "wineel-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(3)
            .SelectMany(path => ReadLinesSafely(path))
            .TakeLast(500)
            .Select(line => SanitizeLogLine(line, userProfile));
        return string.Join(Environment.NewLine, lines);
    }

    private static string SanitizeLogLine(string line, string userProfile)
    {
        if (!string.IsNullOrWhiteSpace(userProfile)) line = line.Replace(userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        return Regex.Replace(line, @"(?i)(?:[a-z]:\\|\\\\)[^|\r\n]+", "<path>");
    }

    private static IEnumerable<string> ReadLinesSafely(string path)
    {
        try { return File.ReadLines(path).ToArray(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
