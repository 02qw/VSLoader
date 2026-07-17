using System.IO;

namespace VSLoader.Services;

public sealed class MainWindowInputDiagnosticLogService
{
    private const string LogFileName = "main-window-input.log";
    public const int MaximumLogLines = 2000;
    private readonly int maximumLogLines;

    public MainWindowInputDiagnosticLogService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VSLoader",
            "logs"), MaximumLogLines)
    {
    }

    internal MainWindowInputDiagnosticLogService(string logDirectory, int maximumLogLines)
    {
        LogPath = Path.Combine(logDirectory, LogFileName);
        this.maximumLogLines = Math.Max(1, maximumLogLines);
    }

    public string LogPath { get; }

    public void Log(string eventName, string details)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} "
                + $"event={Normalize(eventName)} {details?.Trim()}".TrimEnd();
            RollingLogFileWriter.Append(LogPath, line, maximumLogLines);
        }
        catch
        {
            // Window diagnostics must never affect normal input or window state changes.
        }
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Trim().Replace(' ', '_');
    }
}
