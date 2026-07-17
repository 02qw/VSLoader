using System.IO;
using System.Threading;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class AdminUiAutoPasteLogService
{
    private const string LogFileName = "adminui-autopaste.log";
    private static readonly AsyncLocal<long?> CurrentSessionId = new();
    private readonly string logDirectory;

    public AdminUiAutoPasteLogService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VSLoader",
            "logs"))
    {
    }

    public AdminUiAutoPasteLogService(string logDirectory)
    {
        this.logDirectory = logDirectory;
    }

    public IDisposable BeginSession(long sessionId)
    {
        return new SessionScope(sessionId);
    }

    public void LogTaskStart(long sessionId, AdminUiConfig config, int passwordLength)
    {
        WriteLine($"[TaskStart] sessionId={sessionId} timeoutSeconds={config.AutoPasteTimeoutSeconds} pollIntervalMs={AdminUiAutoPasteService.ForegroundPollIntervalMilliseconds} textLength={passwordLength}");
    }

    public void LogTaskCancel(long sessionId, string reason)
    {
        WriteLine($"[TaskCancel] sessionId={sessionId} reason=\"{Escape(reason)}\"");
    }

    public void LogClipboardFallbackStart(long sessionId, AdminUiAutoLoginStatus status, int textLength)
    {
        WriteLine($"[ClipboardFallbackStart] sessionId={sessionId} reasonStatus=\"{status}\" textLength={textLength}");
    }

    public void LogClipboardFallbackCompleted(long sessionId)
    {
        WriteLine($"[ClipboardFallbackCompleted] sessionId={sessionId} success=True");
    }

    public void LogClipboardFallbackFailed(long sessionId, string message)
    {
        WriteLine($"[ClipboardFallbackFailed] sessionId={sessionId} success=False message=\"{Escape(message)}\"");
    }

    public void LogTaskCompleted(long sessionId, AdminUiAutoLoginStatus status, string message)
    {
        WriteLine($"[TaskCompleted] sessionId={sessionId} status=\"{status}\" message=\"{Escape(message)}\"");
    }

    public void LogDialogMatched(ForegroundWindowInfo window, long elapsedMilliseconds)
    {
        WriteLine($"[DialogMatched] handle={window.Handle} title=\"{Escape(window.Title)}\" process=\"{Escape(window.ProcessName)}\" class=\"{Escape(window.ClassName)}\" elapsedMs={elapsedMilliseconds}");
    }

    public void LogStabilityCheck(ForegroundWindowInfo targetWindow, ForegroundWindowInfo? actualWindow, bool matched)
    {
        WriteLine($"[StabilityCheck] expectedHandle={targetWindow.Handle} actualHandle={actualWindow?.Handle ?? IntPtr.Zero} matched={matched}");
    }

    public void LogInputStart(ForegroundWindowInfo targetWindow, int textLength)
    {
        WriteLine($"[InputStart] targetHandle={targetWindow.Handle} textLength={textLength}");
    }

    public void LogTextSent(uint requestedInputCount, uint sentInputCount, long elapsedMilliseconds, int nativeErrorCode)
    {
        WriteLine($"[TextSent] requested={requestedInputCount} sent={sentInputCount} success={sentInputCount == requestedInputCount} elapsedMs={elapsedMilliseconds} nativeErrorCode={nativeErrorCode}");
    }

    public void LogFocusLost(string stage, ForegroundWindowInfo targetWindow, ForegroundWindowInfo? actualWindow)
    {
        WriteLine($"[FocusLost] stage=\"{Escape(stage)}\" expectedHandle={targetWindow.Handle} actualHandle={actualWindow?.Handle ?? IntPtr.Zero}");
    }

    public void LogEnterSent(uint requestedInputCount, uint sentInputCount, long elapsedMilliseconds, int nativeErrorCode)
    {
        WriteLine($"[EnterSent] requested={requestedInputCount} sent={sentInputCount} success={sentInputCount == requestedInputCount} elapsedMs={elapsedMilliseconds} nativeErrorCode={nativeErrorCode}");
    }

    public void LogTimeout(long elapsedMilliseconds)
    {
        WriteLine($"[Timeout] elapsedMs={elapsedMilliseconds}");
    }

    public void LogError(Exception exception)
    {
        var nativeErrorCode = exception is System.ComponentModel.Win32Exception win32Exception
            ? $" nativeErrorCode={win32Exception.NativeErrorCode}"
            : string.Empty;
        WriteLine($"[Error] type=\"{Escape(exception.GetType().Name)}\" message=\"{Escape(exception.Message)}\"{nativeErrorCode}");
    }

    private void WriteLine(string message)
    {
        try
        {
            Directory.CreateDirectory(logDirectory);
            RollingLogFileWriter.Append(
                Path.Combine(logDirectory, LogFileName),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {ApplySessionId(message)}");
        }
        catch
        {
            // Diagnostic logging must never break the AdminUI launch flow.
        }
    }

    private static string ApplySessionId(string message)
    {
        var sessionId = CurrentSessionId.Value;
        if (sessionId is null || message.Contains("sessionId=", StringComparison.Ordinal))
        {
            return message;
        }

        return $"sessionId={sessionId.Value} {message}";
    }

    private static string Escape(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private sealed class SessionScope : IDisposable
    {
        private readonly long? previousSessionId;
        private bool disposed;

        public SessionScope(long sessionId)
        {
            previousSessionId = CurrentSessionId.Value;
            CurrentSessionId.Value = sessionId;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            CurrentSessionId.Value = previousSessionId;
        }
    }
}
