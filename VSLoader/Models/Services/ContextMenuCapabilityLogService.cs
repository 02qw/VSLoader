using System.Diagnostics;
using System.IO;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class ContextMenuCapabilityLogService
{
    private const string LogFileName = "context-menu-capability.log";
    public const int MaximumLogLines = 2000;
    private readonly string logDirectory;
    private readonly int maximumLogLines;

    public ContextMenuCapabilityLogService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VSLoader",
            "logs"), MaximumLogLines)
    {
    }

    public ContextMenuCapabilityLogService(string logDirectory)
        : this(logDirectory, MaximumLogLines)
    {
    }

    internal ContextMenuCapabilityLogService(string logDirectory, int maximumLogLines)
    {
        this.logDirectory = logDirectory;
        this.maximumLogLines = Math.Max(1, maximumLogLines);
    }

    public void Log(
        ContextMenuCapabilityDefinition definition,
        ContextMenuCapabilityExecutionContext context,
        string stage,
        ContextMenuCapabilityExecutionResult result,
        TimeSpan elapsed)
    {
        try
        {
            var scriptHash = string.Equals(definition.Kind, ContextMenuCapabilityKinds.PowerShell, StringComparison.Ordinal)
                ? ContextMenuCapabilityTrustService.ComputeHash(definition)
                : string.Empty;
            var errorSummary = Limit(result.StandardError, 500);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} "
                + $"stage=\"{Escape(stage)}\" id=\"{Escape(definition.Id)}\" name=\"{Escape(definition.Name)}\" "
                + $"kind=\"{Escape(definition.Kind)}\" surface=\"{Escape(context.Surface)}\" "
                + $"shortcut=\"{Escape(context.Shortcut?.Name)}\" target=\"{Escape(context.Shortcut?.TargetPath)}\" "
                + $"success={result.Success} started={result.Started} cancelled={result.Cancelled} timedOut={result.TimedOut} "
                + $"exitCode={result.ExitCode?.ToString() ?? "null"} elapsedMs={(long)elapsed.TotalMilliseconds} "
                + $"scriptHash=\"{scriptHash}\" message=\"{Escape(result.Message)}\" error=\"{Escape(errorSummary)}\"";
            RollingLogFileWriter.Append(Path.Combine(logDirectory, LogFileName), line, maximumLogLines);
        }
        catch
        {
            // Diagnostic logging must never break a context-menu action.
        }
    }

    private static string Limit(string? value, int maximumLength)
    {
        var text = value ?? string.Empty;
        return text.Length <= maximumLength ? text : text[..maximumLength];
    }

    private static string Escape(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
