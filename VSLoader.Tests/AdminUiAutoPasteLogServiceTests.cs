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
    public void LogStart_creates_log_with_safe_config_details()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);

        service.LogStart(new AdminUiConfig
        {
            AutoPastePasswordEnabled = true,
            AutoPasteWindowTitleKeyword = "processor"
        });

        var log = ReadOnlyLogFile();
        Assert.Contains("[Start]", log);
        Assert.Contains("titleKeyword=\"processor\"", log);
        Assert.DoesNotContain("password", log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogPoll_writes_window_title_process_and_match_flags()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);

        service.LogPoll(
            new ForegroundWindowInfo
            {
                Handle = new IntPtr(123),
                Title = "TAOI008.processor",
                ProcessName = "javaw",
                ClassName = "SunAwtFrame"
            },
            titleMatch: true,
            processMatch: true);

        var log = ReadOnlyLogFile();
        Assert.Contains("[Poll]", log);
        Assert.Contains("title=\"TAOI008.processor\"", log);
        Assert.Contains("process=\"javaw\"", log);
        Assert.Contains("class=\"SunAwtFrame\"", log);
        Assert.Contains("titleMatch=True", log);
        Assert.Contains("processMatch=True", log);
    }

    [Fact]
    public void LogKeyboardStep_writes_shortcut_timing_and_result_without_sensitive_text()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);

        service.LogKeyboardStep("SendInput", "Ctrl+V", requestedInputCount: 4, sentInputCount: 4, elapsedMilliseconds: 12, nativeErrorCode: 0);
        service.LogClipboardCheck(expectedLength: 8, clipboardLength: 8, matchesExpectedText: true);

        var log = ReadOnlyLogFile();
        Assert.Contains("[KeyboardStep]", log);
        Assert.Contains("step=\"SendInput\"", log);
        Assert.Contains("shortcut=\"Ctrl+V\"", log);
        Assert.Contains("requested=4", log);
        Assert.Contains("sent=4", log);
        Assert.Contains("elapsedMs=12", log);
        Assert.Contains("[ClipboardCheck]", log);
        Assert.Contains("expectedLength=8", log);
        Assert.Contains("matchesExpectedText=True", log);
        Assert.DoesNotContain("password", log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogStage_writes_stage_target_and_reason()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);

        service.LogStage(
            AdminUiAutoPasteStage.BeforePaste,
            new ForegroundWindowInfo
            {
                Handle = new IntPtr(123),
                Title = "TAOI008.processor",
                ProcessName = "javaw",
                ClassName = "SunAwtDialog"
            },
            "focus check");

        var log = ReadOnlyLogFile();
        Assert.Contains("[Stage]", log);
        Assert.Contains("stage=\"BeforePaste\"", log);
        Assert.Contains("targetHandle=123", log);
        Assert.Contains("class=\"SunAwtDialog\"", log);
        Assert.Contains("reason=\"focus check\"", log);
    }

    [Fact]
    public void LogFocusCheck_writes_expected_actual_and_match_result()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);

        service.LogFocusCheck(
            AdminUiAutoPasteStage.BeforeEnter,
            new ForegroundWindowInfo { Handle = new IntPtr(123), Title = "TAOI008.processor", ProcessName = "javaw", ClassName = "SunAwtDialog" },
            new ForegroundWindowInfo { Handle = new IntPtr(456), Title = "Other", ProcessName = "chrome", ClassName = "Chrome_WidgetWin_1" },
            matched: false);

        var log = ReadOnlyLogFile();
        Assert.Contains("[FocusCheck]", log);
        Assert.Contains("stage=\"BeforeEnter\"", log);
        Assert.Contains("expectedHandle=123", log);
        Assert.Contains("actualHandle=456", log);
        Assert.Contains("matched=False", log);
        Assert.Contains("actualClass=\"Chrome_WidgetWin_1\"", log);
    }

    [Fact]
    public void LogFocusRetry_writes_retry_stage_attempt_and_result()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);
        var target = new ForegroundWindowInfo
        {
            Handle = new IntPtr(123),
            Title = "TAOI008.processor",
            ProcessName = "javaw",
            ClassName = "SunAwtDialog"
        };

        service.LogFocusRetry(AdminUiAutoPasteStage.BeforePaste, target, attempt: 2, setForegroundResult: true);
        service.LogFocusRetryResult(AdminUiAutoPasteStage.BeforePaste, target, success: true, attempts: 2, elapsedMilliseconds: 240);

        var log = ReadOnlyLogFile();
        Assert.Contains("[FocusRetry]", log);
        Assert.Contains("stage=\"BeforePaste\"", log);
        Assert.Contains("targetHandle=123", log);
        Assert.Contains("attempt=2", log);
        Assert.Contains("setForegroundResult=True", log);
        Assert.Contains("[FocusRetryResult]", log);
        Assert.Contains("success=True", log);
        Assert.Contains("attempts=2", log);
        Assert.Contains("elapsedMs=240", log);
    }

    [Fact]
    public void LogInputProtection_writes_mode_result_and_native_error_without_sensitive_text()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);

        service.LogInputProtection(
            new ForegroundWindowInfo
            {
                Handle = new IntPtr(123),
                Title = "TAOI008.processor",
                ProcessName = "javaw",
                ClassName = "SunAwtDialog"
            },
            "Overlay",
            success: true,
            nativeErrorCode: 0);

        var log = ReadOnlyLogFile();
        Assert.Contains("[InputProtection]", log);
        Assert.Contains("targetHandle=123", log);
        Assert.Contains("mode=\"Overlay\"", log);
        Assert.Contains("success=True", log);
        Assert.Contains("nativeErrorCode=0", log);
        Assert.DoesNotContain("password", log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Writes_single_log_file_and_keeps_latest_2000_lines()
    {
        var service = new AdminUiAutoPasteLogService(rootPath);

        for (var index = 1; index <= 2001; index++)
        {
            service.LogTimeout($"message-{index:0000}");
        }

        var files = Directory.GetFiles(rootPath, "*.log");
        Assert.Single(files);
        Assert.Equal("adminui-autopaste.log", Path.GetFileName(files[0]));

        var lines = File.ReadAllLines(files[0]);
        Assert.Equal(2000, lines.Length);
        Assert.DoesNotContain(lines, line => line.Contains("message-0001", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("message-0002", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("message-2001", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }

    private string ReadOnlyLogFile()
    {
        var files = Directory.GetFiles(rootPath, "adminui-autopaste.log");
        Assert.Single(files);
        return File.ReadAllText(files[0]);
    }
}
