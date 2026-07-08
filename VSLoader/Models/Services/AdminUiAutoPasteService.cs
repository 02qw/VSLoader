using VSLoader.Models;

namespace VSLoader.Services;

public sealed class AdminUiAutoPasteService
{
    private readonly Func<ForegroundWindowInfo?> getForegroundWindowInfo;
    private readonly Func<IReadOnlyList<ForegroundWindowInfo>> getTopLevelWindows;
    private readonly Action<ForegroundWindowInfo> sendPasteAndEnter;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly AdminUiAutoPasteLogService logService;

    public AdminUiAutoPasteService()
        : this(new ForegroundWindowService(), new KeyboardInputService())
    {
    }

    public AdminUiAutoPasteService(ForegroundWindowService foregroundWindowService, KeyboardInputService keyboardInputService)
        : this(foregroundWindowService, keyboardInputService, new AdminUiAutoPasteLogService())
    {
    }

    public AdminUiAutoPasteService(
        ForegroundWindowService foregroundWindowService,
        KeyboardInputService keyboardInputService,
        AdminUiAutoPasteLogService logService)
        : this(
            foregroundWindowService.GetForegroundWindowInfo,
            new TopLevelWindowService().GetTopLevelWindows,
            window => keyboardInputService.SendPasteAndEnter(window, logService),
            Task.Delay,
            logService)
    {
    }

    internal AdminUiAutoPasteService(
        Func<ForegroundWindowInfo?> getForegroundWindowInfo,
        Action sendPasteAndEnter,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        AdminUiAutoPasteLogService? logService = null)
        : this(getForegroundWindowInfo, Array.Empty<ForegroundWindowInfo>, _ => sendPasteAndEnter(), delayAsync, logService)
    {
    }

    internal AdminUiAutoPasteService(
        Func<ForegroundWindowInfo?> getForegroundWindowInfo,
        Action<ForegroundWindowInfo> sendPasteAndEnter,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        AdminUiAutoPasteLogService? logService = null)
        : this(getForegroundWindowInfo, Array.Empty<ForegroundWindowInfo>, sendPasteAndEnter, delayAsync, logService)
    {
    }

    internal AdminUiAutoPasteService(
        Func<ForegroundWindowInfo?> getForegroundWindowInfo,
        Func<IReadOnlyList<ForegroundWindowInfo>> getTopLevelWindows,
        Action<ForegroundWindowInfo> sendPasteAndEnter,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        AdminUiAutoPasteLogService? logService = null)
    {
        this.getForegroundWindowInfo = getForegroundWindowInfo;
        this.getTopLevelWindows = getTopLevelWindows;
        this.sendPasteAndEnter = sendPasteAndEnter;
        this.delayAsync = delayAsync;
        this.logService = logService ?? new AdminUiAutoPasteLogService();
    }

    public async Task<AdminUiAutoPasteResult> TryPasteAsync(AdminUiConfig config, CancellationToken cancellationToken = default)
    {
        if (!config.AutoPastePasswordEnabled)
        {
            return AdminUiAutoPasteResult.Fail("未启用自动粘贴。");
        }

        logService.LogStart(config);
        await delayAsync(GetInitialDelay(config), cancellationToken);

        var timeout = TimeSpan.FromSeconds(Math.Clamp(config.AutoPasteTimeoutSeconds, 1, 60));
        var pollInterval = TimeSpan.FromMilliseconds(Math.Clamp(config.AutoPastePollIntervalMilliseconds, 50, 2000));
        var stopAt = DateTimeOffset.Now + timeout;

        while (DateTimeOffset.Now < stopAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var window = FindAdminUiDialogWindow(config);
            if (window is null)
            {
                logService.LogPoll(null, titleMatch: false, processMatch: false, classMatch: false);
                await delayAsync(pollInterval, cancellationToken);
                continue;
            }

            var match = EvaluateAdminUiDialogWindow(window, config);
            logService.LogPoll(window, match.TitleMatch, match.ProcessMatch, match.ClassMatch);
            if (match.IsMatch)
            {
                logService.LogSend(window);
                try
                {
                    sendPasteAndEnter(window);
                    logService.LogSendCompleted(window);
                    return AdminUiAutoPasteResult.Ok(window);
                }
                catch (Exception ex)
                {
                    logService.LogError(ex);
                    return AdminUiAutoPasteResult.Fail($"自动粘贴按键发送失败：{ex.Message}");
                }
            }

            await delayAsync(pollInterval, cancellationToken);
        }

        const string timeoutMessage = "等待超时，未检测到 AdminUI 登录窗口。";
        logService.LogTimeout(timeoutMessage);
        return AdminUiAutoPasteResult.Fail(timeoutMessage);
    }

    internal static bool IsAdminUiWindow(ForegroundWindowInfo? window, AdminUiConfig config)
    {
        return EvaluateAdminUiWindow(window, config).IsMatch;
    }

    internal static bool IsAdminUiDialogWindow(ForegroundWindowInfo? window, AdminUiConfig config)
    {
        return EvaluateAdminUiDialogWindow(window, config).IsMatch;
    }

    internal static AdminUiWindowMatch EvaluateAdminUiWindow(ForegroundWindowInfo? window, AdminUiConfig config)
    {
        if (window is null
            || string.IsNullOrWhiteSpace(window.Title)
            || string.IsNullOrWhiteSpace(window.ProcessName))
        {
            return new AdminUiWindowMatch(false, false, false, false);
        }

        var titleKeyword = config.AutoPasteWindowTitleKeyword?.Trim();
        var titleMatch = !string.IsNullOrWhiteSpace(titleKeyword)
            && window.Title.Contains(titleKeyword, StringComparison.OrdinalIgnoreCase);

        var allowedProcesses = ParseProcessNames(config.AutoPasteProcessNames);
        var processMatch = allowedProcesses.Contains(window.ProcessName.Trim(), StringComparer.OrdinalIgnoreCase);
        var classMatch = IsSafeAdminUiWindowClass(window.ClassName);
        return new AdminUiWindowMatch(titleMatch && processMatch && classMatch, titleMatch, processMatch, classMatch);
    }

    internal static AdminUiWindowMatch EvaluateAdminUiDialogWindow(ForegroundWindowInfo? window, AdminUiConfig config)
    {
        if (window is null
            || string.IsNullOrWhiteSpace(window.Title)
            || string.IsNullOrWhiteSpace(window.ProcessName))
        {
            return new AdminUiWindowMatch(false, false, false, false);
        }

        var baseMatch = EvaluateAdminUiWindow(window, config);
        var classMatch = IsStrictDialogWindow(window);
        return new AdminUiWindowMatch(
            baseMatch.TitleMatch && baseMatch.ProcessMatch && classMatch,
            baseMatch.TitleMatch,
            baseMatch.ProcessMatch,
            classMatch);
    }

    private ForegroundWindowInfo? FindAdminUiDialogWindow(AdminUiConfig config)
    {
        IReadOnlyList<ForegroundWindowInfo> windows;
        try
        {
            windows = getTopLevelWindows();
        }
        catch (Exception ex)
        {
            logService.LogError(ex);
            return null;
        }

        logService.LogWindowScanStart(windows.Count);
        ForegroundWindowInfo? match = null;
        foreach (var window in windows)
        {
            var result = EvaluateAdminUiDialogWindow(window, config);
            if (result.IsMatch)
            {
                match = window;
                logService.LogWindowMatch(window);
                break;
            }
        }

        logService.LogWindowScanEnd(windows.Count, match is null ? 0 : 1);
        return match;
    }

    private static bool IsSafeAdminUiWindowClass(string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return true;
        }

        return !string.Equals(className.Trim(), "SunAwtFrame", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStrictDialogWindow(ForegroundWindowInfo? window)
    {
        return string.Equals(window?.ClassName?.Trim(), "SunAwtDialog", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan GetInitialDelay(AdminUiConfig config)
    {
        return TimeSpan.FromMilliseconds(Math.Clamp(config.AutoPasteInitialDelayMilliseconds, 0, 30000));
    }

    private static HashSet<string> ParseProcessNames(string? processNames)
    {
        return (processNames ?? string.Empty)
            .Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

internal sealed record AdminUiWindowMatch(bool IsMatch, bool TitleMatch, bool ProcessMatch, bool ClassMatch);
