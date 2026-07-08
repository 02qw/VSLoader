using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class AdminUiAutoPasteServiceTests
{
    [Fact]
    public void IsAdminUiWindow_matches_title_keyword_and_allowed_process()
    {
        var config = new AdminUiConfig
        {
            AutoPasteWindowTitleKeyword = "znt client",
            AutoPasteProcessNames = "java;javaw;javaws"
        };
        var window = new ForegroundWindowInfo
        {
            Handle = new IntPtr(123),
            Title = "ZNT CLIENT - TYLC001",
            ProcessName = "javaw"
        };

        Assert.True(AdminUiAutoPasteService.IsAdminUiWindow(window, config));
    }

    [Theory]
    [InlineData("", "javaw")]
    [InlineData("Other Window", "javaw")]
    [InlineData("znt client", "chrome")]
    public void IsAdminUiWindow_rejects_non_matching_windows(string title, string processName)
    {
        var config = new AdminUiConfig
        {
            AutoPasteWindowTitleKeyword = "znt client",
            AutoPasteProcessNames = "java;javaw;javaws"
        };
        var window = new ForegroundWindowInfo
        {
            Handle = new IntPtr(123),
            Title = title,
            ProcessName = processName
        };

        Assert.False(AdminUiAutoPasteService.IsAdminUiWindow(window, config));
    }

    [Fact]
    public void IsAdminUiWindow_rejects_null_window()
    {
        Assert.False(AdminUiAutoPasteService.IsAdminUiWindow(null, new AdminUiConfig()));
    }

    [Fact]
    public void IsAdminUiWindow_matches_semicolon_process_list_case_insensitively()
    {
        var config = new AdminUiConfig
        {
            AutoPasteWindowTitleKeyword = "client",
            AutoPasteProcessNames = "java;JAVAWS;custom"
        };
        var window = new ForegroundWindowInfo
        {
            Handle = new IntPtr(123),
            Title = "znt client",
            ProcessName = "javaws"
        };

        Assert.True(AdminUiAutoPasteService.IsAdminUiWindow(window, config));
    }

    [Fact]
    public void IsAdminUiWindow_matches_real_processor_login_title_with_default_keyword()
    {
        var config = new AdminUiConfig();
        var window = new ForegroundWindowInfo
        {
            Handle = new IntPtr(123),
            Title = "TAOI008.processor",
            ProcessName = "javaw"
        };

        Assert.True(AdminUiAutoPasteService.IsAdminUiWindow(window, config));
    }

    [Fact]
    public void IsAdminUiWindow_rejects_java_awt_main_frame_to_avoid_pasting_before_login_dialog()
    {
        var config = new AdminUiConfig();
        var window = new ForegroundWindowInfo
        {
            Handle = new IntPtr(123),
            Title = "TAOI008.processor",
            ProcessName = "javaw",
            ClassName = "SunAwtFrame"
        };

        Assert.False(AdminUiAutoPasteService.IsAdminUiWindow(window, config));
    }

    [Fact]
    public void IsAdminUiWindow_matches_java_awt_login_dialog()
    {
        var config = new AdminUiConfig();
        var window = new ForegroundWindowInfo
        {
            Handle = new IntPtr(123),
            Title = "TAOI008.processor",
            ProcessName = "javaw",
            ClassName = "SunAwtDialog"
        };

        Assert.True(AdminUiAutoPasteService.IsAdminUiWindow(window, config));
    }

    [Fact]
    public void IsAdminUiDialogWindow_requires_java_awt_login_dialog()
    {
        var config = new AdminUiConfig();
        var dialog = new ForegroundWindowInfo
        {
            Handle = new IntPtr(123),
            Title = "TAOI008.processor",
            ProcessName = "javaw",
            ClassName = "SunAwtDialog"
        };
        var frame = new ForegroundWindowInfo
        {
            Handle = new IntPtr(456),
            Title = "TAOI008.processor",
            ProcessName = "javaw",
            ClassName = "SunAwtFrame"
        };

        Assert.True(AdminUiAutoPasteService.IsAdminUiDialogWindow(dialog, config));
        Assert.False(AdminUiAutoPasteService.IsAdminUiDialogWindow(frame, config));
    }

    [Fact]
    public async Task TryPasteAsync_finds_background_sun_awt_dialog_when_foreground_is_other_app()
    {
        var foreground = new ForegroundWindowInfo
        {
            Handle = new IntPtr(1),
            Title = "VSCode",
            ProcessName = "Code",
            ClassName = "Chrome_WidgetWin_1"
        };
        var dialog = new ForegroundWindowInfo
        {
            Handle = new IntPtr(9),
            Title = "TAOI008.processor",
            ProcessName = "javaw",
            ClassName = "SunAwtDialog"
        };
        ForegroundWindowInfo? sentWindow = null;
        Func<IReadOnlyList<ForegroundWindowInfo>> getTopLevelWindows = () => new[]
        {
            new ForegroundWindowInfo { Handle = new IntPtr(8), Title = "TAOI008.processor", ProcessName = "javaw", ClassName = "SunAwtFrame" },
            new ForegroundWindowInfo { Handle = new IntPtr(7), Title = "Browser", ProcessName = "chrome", ClassName = "Chrome_WidgetWin_1" },
            dialog
        };
        var service = new AdminUiAutoPasteService(
            () => foreground,
            getTopLevelWindows,
            window => sentWindow = window,
            (_, _) => Task.CompletedTask);

        var result = await service.TryPasteAsync(new AdminUiConfig
        {
            AutoPastePasswordEnabled = true,
            AutoPasteInitialDelayMilliseconds = 0,
            AutoPastePollIntervalMilliseconds = 1,
            AutoPasteTimeoutSeconds = 1
        });

        Assert.True(result.Success, result.Message);
        Assert.Same(dialog, sentWindow);
        Assert.Same(dialog, result.MatchedWindow);
    }

    [Fact]
    public async Task TryPasteAsync_logs_scan_summary_and_matched_dialog_without_noisy_candidates()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        try
        {
            var dialog = new ForegroundWindowInfo
            {
                Handle = new IntPtr(9),
                Title = "TAOI008.processor",
                ProcessName = "javaw",
                ClassName = "SunAwtDialog"
            };
            var service = new AdminUiAutoPasteService(
                () => new ForegroundWindowInfo { Handle = new IntPtr(1), Title = "VSCode", ProcessName = "Code", ClassName = "Chrome_WidgetWin_1" },
                () => new[]
                {
                    new ForegroundWindowInfo { Handle = new IntPtr(8), Title = "TAOI008.processor", ProcessName = "javaw", ClassName = "SunAwtFrame" },
                    new ForegroundWindowInfo { Handle = new IntPtr(7), Title = "Browser", ProcessName = "chrome", ClassName = "Chrome_WidgetWin_1" },
                    dialog
                },
                _ => { },
                (_, _) => Task.CompletedTask,
                new AdminUiAutoPasteLogService(rootPath));

            var result = await service.TryPasteAsync(new AdminUiConfig
            {
                AutoPastePasswordEnabled = true,
                AutoPasteInitialDelayMilliseconds = 0,
                AutoPastePollIntervalMilliseconds = 1,
                AutoPasteTimeoutSeconds = 1
            });

            Assert.True(result.Success, result.Message);
            var log = File.ReadAllText(Path.Combine(rootPath, "adminui-autopaste.log"));
            Assert.Contains("[WindowScanStart]", log);
            Assert.Contains("[WindowScanEnd]", log);
            Assert.Contains("[WindowMatch]", log);
            Assert.Contains("handle=9", log);
            Assert.DoesNotContain("[WindowCandidate]", log);
            Assert.DoesNotContain("Browser", log);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task TryPasteAsync_does_not_use_foreground_window_fallback_without_sun_awt_dialog()
    {
        var sendCount = 0;
        var service = new AdminUiAutoPasteService(
            () => new ForegroundWindowInfo
            {
                Handle = new IntPtr(2),
                Title = "TAOI008.processor",
                ProcessName = "javaw",
                ClassName = string.Empty
            },
            () => Array.Empty<ForegroundWindowInfo>(),
            _ => sendCount++,
            (_, _) => Task.CompletedTask);

        var result = await service.TryPasteAsync(new AdminUiConfig
        {
            AutoPastePasswordEnabled = true,
            AutoPasteInitialDelayMilliseconds = 0,
            AutoPastePollIntervalMilliseconds = 1,
            AutoPasteTimeoutSeconds = 1
        });

        Assert.False(result.Success);
        Assert.Equal(0, sendCount);
        Assert.Contains("未检测到 AdminUI 登录窗口", result.Message);
    }

    [Fact]
    public async Task TryPasteAsync_waits_for_awt_dialog_instead_of_sending_to_main_frame()
    {
        var dialog = new ForegroundWindowInfo
        {
            Handle = new IntPtr(3),
            Title = "TAOI008.processor",
            ProcessName = "javaw",
            ClassName = "SunAwtDialog"
        };
        var scans = new Queue<IReadOnlyList<ForegroundWindowInfo>>([
            [new ForegroundWindowInfo { Handle = new IntPtr(1), Title = "TAOI008.processor", ProcessName = "javaw", ClassName = "SunAwtFrame" }],
            [dialog]
        ]);
        ForegroundWindowInfo? sentWindow = null;
        var service = new AdminUiAutoPasteService(
            () => null,
            () => scans.Count > 0 ? scans.Dequeue() : [dialog],
            window => sentWindow = window,
            (_, _) => Task.CompletedTask);

        var result = await service.TryPasteAsync(new AdminUiConfig
        {
            AutoPastePasswordEnabled = true,
            AutoPasteInitialDelayMilliseconds = 0,
            AutoPastePollIntervalMilliseconds = 1,
            AutoPasteTimeoutSeconds = 1
        });

        Assert.True(result.Success, result.Message);
        Assert.Same(dialog, sentWindow);
    }

    [Fact]
    public async Task TryPasteAsync_sends_to_matched_adminui_window()
    {
        var matchedWindow = new ForegroundWindowInfo
        {
            Handle = new IntPtr(987),
            Title = "TAOI008.processor",
            ProcessName = "javaw",
            ClassName = "SunAwtDialog"
        };
        ForegroundWindowInfo? sentWindow = null;
        var service = new AdminUiAutoPasteService(
            () => null,
            () => [matchedWindow],
            window => sentWindow = window,
            (_, _) => Task.CompletedTask);

        var result = await service.TryPasteAsync(new AdminUiConfig
        {
            AutoPastePasswordEnabled = true,
            AutoPasteInitialDelayMilliseconds = 0,
            AutoPastePollIntervalMilliseconds = 1,
            AutoPasteTimeoutSeconds = 1
        });

        Assert.True(result.Success, result.Message);
        Assert.Same(matchedWindow, sentWindow);
    }

    [Fact]
    public async Task TryPasteAsync_returns_failure_when_keyboard_sender_fails()
    {
        var dialog = new ForegroundWindowInfo
        {
            Handle = new IntPtr(987),
            Title = "TAOI008.processor",
            ProcessName = "javaw",
            ClassName = "SunAwtDialog"
        };
        var service = new AdminUiAutoPasteService(
            () => null,
            () => [dialog],
            _ => throw new InvalidOperationException("send failed"),
            (_, _) => Task.CompletedTask);

        var result = await service.TryPasteAsync(new AdminUiConfig
        {
            AutoPastePasswordEnabled = true,
            AutoPasteInitialDelayMilliseconds = 0,
            AutoPastePollIntervalMilliseconds = 1,
            AutoPasteTimeoutSeconds = 1
        });

        Assert.False(result.Success);
        Assert.Contains("自动粘贴按键发送失败", result.Message);
    }

    [Fact]
    public async Task TryPasteAsync_times_out_without_sending_when_window_never_matches()
    {
        var sendCount = 0;
        var service = new AdminUiAutoPasteService(
            () => new ForegroundWindowInfo { Handle = new IntPtr(1), Title = "Browser", ProcessName = "chrome" },
            () => sendCount++,
            (_, _) => Task.CompletedTask);

        var result = await service.TryPasteAsync(new AdminUiConfig
        {
            AutoPastePasswordEnabled = true,
            AutoPasteInitialDelayMilliseconds = 0,
            AutoPastePollIntervalMilliseconds = 1,
            AutoPasteTimeoutSeconds = 1
        });

        Assert.False(result.Success);
        Assert.Equal(0, sendCount);
        Assert.Contains("未检测到 AdminUI 登录窗口", result.Message);
    }

    [Fact]
    public async Task TryPasteAsync_does_not_send_when_disabled()
    {
        var sendCount = 0;
        var service = new AdminUiAutoPasteService(
            () => new ForegroundWindowInfo { Handle = new IntPtr(1), Title = "znt client", ProcessName = "javaw" },
            () => sendCount++,
            (_, _) => Task.CompletedTask);

        var result = await service.TryPasteAsync(new AdminUiConfig
        {
            AutoPastePasswordEnabled = false
        });

        Assert.False(result.Success);
        Assert.Equal(0, sendCount);
        Assert.Contains("未启用", result.Message);
    }
}
