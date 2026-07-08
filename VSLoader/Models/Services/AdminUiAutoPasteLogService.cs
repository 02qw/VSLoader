using System.IO;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class AdminUiAutoPasteLogService
{
    private const string LogFileName = "adminui-autopaste.log";

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

    public void LogStart(AdminUiConfig config)
    {
        WriteLine($"[Start] enabled={config.AutoPastePasswordEnabled} titleKeyword=\"{Escape(config.AutoPasteWindowTitleKeyword)}\" processNames=\"{Escape(config.AutoPasteProcessNames)}\" timeoutSeconds={config.AutoPasteTimeoutSeconds} initialDelayMs={config.AutoPasteInitialDelayMilliseconds} pollIntervalMs={config.AutoPastePollIntervalMilliseconds}");
    }

    public void LogPoll(ForegroundWindowInfo? window, bool titleMatch, bool processMatch, bool classMatch = true)
    {
        if (window is null)
        {
            WriteLine($"[Poll] window=null titleMatch={titleMatch} processMatch={processMatch} classMatch={classMatch}");
            return;
        }

        WriteLine($"[Poll] handle={window.Handle} title=\"{Escape(window.Title)}\" process=\"{Escape(window.ProcessName)}\" class=\"{Escape(window.ClassName)}\" titleMatch={titleMatch} processMatch={processMatch} classMatch={classMatch}");
    }

    public void LogWindowScanStart(int candidateCount)
    {
        WriteLine($"[WindowScanStart] candidateCount={candidateCount}");
    }

    public void LogWindowCandidate(ForegroundWindowInfo window, bool titleMatch, bool processMatch, bool classMatch)
    {
        WriteLine($"[WindowCandidate] handle={window.Handle} title=\"{Escape(window.Title)}\" process=\"{Escape(window.ProcessName)}\" class=\"{Escape(window.ClassName)}\" titleMatch={titleMatch} processMatch={processMatch} classMatch={classMatch}");
    }

    public void LogWindowMatch(ForegroundWindowInfo window)
    {
        WriteLine($"[WindowMatch] handle={window.Handle} title=\"{Escape(window.Title)}\" process=\"{Escape(window.ProcessName)}\" class=\"{Escape(window.ClassName)}\"");
    }

    public void LogWindowScanEnd(int candidateCount, int matchCount)
    {
        WriteLine($"[WindowScanEnd] candidateCount={candidateCount} matchCount={matchCount}");
    }

    public void LogSend(ForegroundWindowInfo window)
    {
        WriteLine($"[Send] handle={window.Handle} title=\"{Escape(window.Title)}\" process=\"{Escape(window.ProcessName)}\" class=\"{Escape(window.ClassName)}\"");
    }

    public void LogSendCompleted(ForegroundWindowInfo window)
    {
        WriteLine($"[SendCompleted] handle={window.Handle} title=\"{Escape(window.Title)}\" process=\"{Escape(window.ProcessName)}\" class=\"{Escape(window.ClassName)}\"");
    }

    public void LogClipboardCheck(int expectedLength, int clipboardLength, bool matchesExpectedText)
    {
        WriteLine($"[ClipboardCheck] expectedLength={expectedLength} clipboardLength={clipboardLength} matchesExpectedText={matchesExpectedText}");
    }

    internal void LogStage(AdminUiAutoPasteStage stage, ForegroundWindowInfo targetWindow, string reason = "")
    {
        var reasonPart = string.IsNullOrWhiteSpace(reason)
            ? string.Empty
            : $" reason=\"{Escape(reason)}\"";
        WriteLine($"[Stage] stage=\"{stage}\" targetHandle={targetWindow.Handle} title=\"{Escape(targetWindow.Title)}\" process=\"{Escape(targetWindow.ProcessName)}\" class=\"{Escape(targetWindow.ClassName)}\"{reasonPart}");
    }

    internal void LogFocusCheck(AdminUiAutoPasteStage stage, ForegroundWindowInfo targetWindow, ForegroundWindowInfo? actualWindow, bool matched)
    {
        if (actualWindow is null)
        {
            WriteLine($"[FocusCheck] stage=\"{stage}\" expectedHandle={targetWindow.Handle} actualHandle=0 matched={matched} actualTitle=\"\" actualProcess=\"\" actualClass=\"\"");
            return;
        }

        WriteLine($"[FocusCheck] stage=\"{stage}\" expectedHandle={targetWindow.Handle} actualHandle={actualWindow.Handle} matched={matched} actualTitle=\"{Escape(actualWindow.Title)}\" actualProcess=\"{Escape(actualWindow.ProcessName)}\" actualClass=\"{Escape(actualWindow.ClassName)}\"");
    }

    internal void LogFocusRetry(AdminUiAutoPasteStage stage, ForegroundWindowInfo targetWindow, int attempt, bool setForegroundResult)
    {
        WriteLine($"[FocusRetry] stage=\"{stage}\" targetHandle={targetWindow.Handle} attempt={attempt} setForegroundResult={setForegroundResult}");
    }

    internal void LogFocusRetryResult(AdminUiAutoPasteStage stage, ForegroundWindowInfo targetWindow, bool success, int attempts, long elapsedMilliseconds)
    {
        WriteLine($"[FocusRetryResult] stage=\"{stage}\" targetHandle={targetWindow.Handle} success={success} attempts={attempts} elapsedMs={elapsedMilliseconds}");
    }

    internal void LogInputBlock(ForegroundWindowInfo targetWindow, bool requestedBlock, bool success, int nativeErrorCode = 0)
    {
        WriteLine($"[InputBlock] targetHandle={targetWindow.Handle} requestedBlock={requestedBlock} success={success} nativeErrorCode={nativeErrorCode}");
    }

    internal void LogInputProtection(ForegroundWindowInfo targetWindow, string mode, bool success, int nativeErrorCode = 0)
    {
        WriteLine($"[InputProtection] targetHandle={targetWindow.Handle} mode=\"{Escape(mode)}\" success={success} nativeErrorCode={nativeErrorCode}");
    }

    public void LogKeyboardPlan(ForegroundWindowInfo targetWindow, int focusSettleMilliseconds, int pasteBeforeEnterDelayMilliseconds, int inputStructSize)
    {
        WriteLine($"[KeyboardPlan] targetHandle={targetWindow.Handle} title=\"{Escape(targetWindow.Title)}\" process=\"{Escape(targetWindow.ProcessName)}\" class=\"{Escape(targetWindow.ClassName)}\" shortcuts=\"Ctrl+V,Enter\" focusSettleMs={focusSettleMilliseconds} pasteBeforeEnterDelayMs={pasteBeforeEnterDelayMilliseconds} inputStructSize={inputStructSize}");
    }

    public void LogKeyboardForeground(string stage, ForegroundWindowInfo? window)
    {
        if (window is null)
        {
            WriteLine($"[KeyboardForeground] stage=\"{Escape(stage)}\" window=null");
            return;
        }

        WriteLine($"[KeyboardForeground] stage=\"{Escape(stage)}\" handle={window.Handle} title=\"{Escape(window.Title)}\" process=\"{Escape(window.ProcessName)}\" class=\"{Escape(window.ClassName)}\"");
    }

    public void LogKeyboardStep(string step, string shortcut, uint requestedInputCount, uint sentInputCount, long elapsedMilliseconds, int nativeErrorCode)
    {
        WriteLine($"[KeyboardStep] step=\"{Escape(step)}\" shortcut=\"{Escape(shortcut)}\" requested={requestedInputCount} sent={sentInputCount} success={sentInputCount == requestedInputCount} elapsedMs={elapsedMilliseconds} nativeErrorCode={nativeErrorCode}");
    }

    public void LogKeyboardDelay(string reason, int delayMilliseconds)
    {
        WriteLine($"[KeyboardDelay] reason=\"{Escape(reason)}\" delayMs={delayMilliseconds}");
    }

    public void LogTimeout(string message)
    {
        WriteLine($"[Timeout] message=\"{Escape(message)}\"");
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
            var filePath = Path.Combine(logDirectory, LogFileName);
            RollingLogFileWriter.Append(filePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
        }
        catch
        {
            // Diagnostic logging must never break the AdminUI launch flow.
        }
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
