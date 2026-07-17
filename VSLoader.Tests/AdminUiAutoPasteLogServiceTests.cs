using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class AdminUiAutoPasteLogServiceTests : IDisposable
{
    private readonly string rootPath;

    public AdminUiAutoPasteLogServiceTests()
    {
        rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
    }

    [Fact]
    public void Event_logs_include_session_and_never_include_plain_password()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);
        var target = CreateDialog(123);

        service.LogTaskStart(42, new AdminUiConfig { AutoPasteTimeoutSeconds = 8 }, passwordLength: 6);
        using (service.BeginSession(42))
        {
            service.LogDialogMatched(target, elapsedMilliseconds: 120);
            service.LogInputStart(target, textLength: 6);
            service.LogTextSent(requestedInputCount: 12, sentInputCount: 12, elapsedMilliseconds: 1, nativeErrorCode: 0);
            service.LogEnterSent(requestedInputCount: 2, sentInputCount: 2, elapsedMilliseconds: 1, nativeErrorCode: 0);
        }
        service.LogTaskCompleted(42, AdminUiAutoLoginStatus.InputSubmitted, "completed");

        var log = File.ReadAllText(LogPath);
        Assert.Contains("[TaskStart]", log);
        Assert.Contains("sessionId=42", log);
        Assert.Contains("[DialogMatched]", log);
        Assert.Contains("[InputStart]", log);
        Assert.Contains("[TextSent]", log);
        Assert.Contains("[EnterSent]", log);
        Assert.Contains("status=\"InputSubmitted\"", log);
        Assert.DoesNotContain("secret", log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Focus_loss_and_timeout_are_event_logs_without_poll_noise()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);
        var target = CreateDialog(123);
        var other = new ForegroundWindowInfo { Handle = new IntPtr(456), Title = "Other", ProcessName = "chrome", ClassName = "Chrome_WidgetWin_1" };

        using (service.BeginSession(8))
        {
            service.LogStabilityCheck(target, other, matched: false);
            service.LogFocusLost("BeforeEnter", target, other);
            service.LogTimeout(elapsedMilliseconds: 12000);
        }

        var log = File.ReadAllText(LogPath);
        Assert.Contains("[StabilityCheck]", log);
        Assert.Contains("[FocusLost]", log);
        Assert.Contains("[Timeout]", log);
        Assert.DoesNotContain("[Poll]", log);
        Assert.DoesNotContain("[WindowScan", log);
        Assert.DoesNotContain("[FocusRetry", log);
        Assert.DoesNotContain("[InputBlock]", log);
    }

    [Fact]
    public void Clipboard_fallback_logs_result_without_password_text()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);

        service.LogClipboardFallbackStart(7, AdminUiAutoLoginStatus.TimedOut, textLength: 6);
        service.LogClipboardFallbackCompleted(7);
        service.LogClipboardFallbackFailed(8, "OpenClipboard Failed");

        var log = File.ReadAllText(LogPath);
        Assert.Contains("[ClipboardFallbackStart]", log);
        Assert.Contains("reasonStatus=\"TimedOut\"", log);
        Assert.Contains("textLength=6", log);
        Assert.Contains("[ClipboardFallbackCompleted]", log);
        Assert.Contains("[ClipboardFallbackFailed]", log);
        Assert.Contains("OpenClipboard Failed", log);
        Assert.DoesNotContain("secret", log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Writes_single_log_file_and_keeps_latest_2000_lines()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);

        for (var index = 1; index <= 2001; index++)
        {
            service.LogTimeout(index);
        }

        var files = Directory.GetFiles(rootPath, "*.log");
        Assert.Single(files);
        var lines = File.ReadAllLines(files[0]);
        Assert.Equal(2000, lines.Length);
        Assert.DoesNotContain(lines, line => line.EndsWith("[Timeout] elapsedMs=1", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("elapsedMs=2001", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }

    private string LogPath => Path.Combine(rootPath, "adminui-autopaste.log");

    private static ForegroundWindowInfo CreateDialog(int handle)
    {
        return new ForegroundWindowInfo
        {
            Handle = new IntPtr(handle),
            Title = "TAOI008.processor",
            ProcessName = "javaw",
            ClassName = "SunAwtDialog"
        };
    }
}
