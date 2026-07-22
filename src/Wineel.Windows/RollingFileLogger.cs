using System.IO;

namespace Wineel;

public sealed class RollingFileLogger
{
    private readonly object _gate = new();
    private string? _directory;
    public static RollingFileLogger Instance { get; } = new();

    public void Configure(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        Prune();
    }

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("WARN", message);
    public void Error(string message, Exception? exception = null) => Write("ERROR", exception is null ? message : $"{message} | {exception.GetType().Name}: {exception.Message}");

    private void Write(string level, string message)
    {
        if (_directory is null) return;
        var line = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
        lock (_gate)
        {
            try { File.AppendAllText(Path.Combine(_directory, $"wineel-{DateTime.Now:yyyyMMdd}.log"), line); }
            catch (IOException) { }
        }
    }

    private void Prune()
    {
        if (_directory is null) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(_directory, "wineel-*.log").OrderByDescending(File.GetLastWriteTimeUtc).Skip(7))
                File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(exception.ToString());
        }
    }
}
