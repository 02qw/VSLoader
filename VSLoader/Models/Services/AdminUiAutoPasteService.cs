using System.Diagnostics;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class AdminUiAutoPasteService
{
    internal const int ForegroundPollIntervalMilliseconds = 100;
    internal const int DialogStabilityDelayMilliseconds = 30;

    private readonly Func<ForegroundWindowInfo?> getForegroundWindowInfo;
    private readonly Func<ForegroundWindowInfo, string, CancellationToken, Task<AdminUiAutoPasteResult>> sendTextAndEnterAsync;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly Func<DateTimeOffset> getNow;
    private readonly AdminUiAutoPasteLogService logService;

    public AdminUiAutoPasteService()
        : this(new ForegroundWindowService(), new KeyboardInputService(), new AdminUiAutoPasteLogService())
    {
    }

    public AdminUiAutoPasteService(
        ForegroundWindowService foregroundWindowService,
        KeyboardInputService keyboardInputService,
        AdminUiAutoPasteLogService logService)
        : this(
            foregroundWindowService.GetForegroundWindowInfo,
            (window, password, token) => keyboardInputService.SendTextAndEnterIfFocusedAsync(window, password, token, logService),
            Task.Delay,
            () => DateTimeOffset.UtcNow,
            logService)
    {
    }

    internal AdminUiAutoPasteService(
        Func<ForegroundWindowInfo?> getForegroundWindowInfo,
        Func<ForegroundWindowInfo, string, CancellationToken, Task<AdminUiAutoPasteResult>> sendTextAndEnterAsync,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        Func<DateTimeOffset>? getNow = null,
        AdminUiAutoPasteLogService? logService = null)
    {
        this.getForegroundWindowInfo = getForegroundWindowInfo;
        this.sendTextAndEnterAsync = sendTextAndEnterAsync;
        this.delayAsync = delayAsync;
        this.getNow = getNow ?? (() => DateTimeOffset.UtcNow);
        this.logService = logService ?? new AdminUiAutoPasteLogService();
    }

    public async Task<AdminUiAutoPasteResult> TryAutoLoginAsync(
        AdminUiConfig config,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!config.AutoPastePasswordEnabled)
        {
            return AdminUiAutoPasteResult.InputFailed("未启用自动登录。");
        }

        if (string.IsNullOrEmpty(password))
        {
            return AdminUiAutoPasteResult.PasswordEmpty();
        }

        var stopwatch = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(config.AutoPasteTimeoutSeconds, 1, 60));
        var stopAt = getNow() + timeout;

        while (getNow() < stopAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var window = getForegroundWindowInfo();
            if (IsAdminUiDialogWindow(window, config))
            {
                logService.LogDialogMatched(window!, stopwatch.ElapsedMilliseconds);
                await delayAsync(TimeSpan.FromMilliseconds(DialogStabilityDelayMilliseconds), cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                var stableWindow = getForegroundWindowInfo();
                var stable = stableWindow?.Handle == window!.Handle;
                logService.LogStabilityCheck(window, stableWindow, stable);
                if (!stable)
                {
                    logService.LogFocusLost("StabilityCheck", window, stableWindow);
                    return AdminUiAutoPasteResult.FocusLostBeforeInput(window);
                }

                return await sendTextAndEnterAsync(window, password, cancellationToken).ConfigureAwait(false);
            }

            await delayAsync(TimeSpan.FromMilliseconds(ForegroundPollIntervalMilliseconds), cancellationToken).ConfigureAwait(false);
        }

        stopwatch.Stop();
        logService.LogTimeout(stopwatch.ElapsedMilliseconds);
        return AdminUiAutoPasteResult.TimedOut();
    }

    internal static bool IsAdminUiDialogWindow(ForegroundWindowInfo? window, AdminUiConfig config)
    {
        if (window is null
            || window.Handle == IntPtr.Zero
            || string.IsNullOrWhiteSpace(window.Title)
            || string.IsNullOrWhiteSpace(window.ProcessName))
        {
            return false;
        }

        var titleKeyword = config.AutoPasteWindowTitleKeyword?.Trim();
        var titleMatch = !string.IsNullOrWhiteSpace(titleKeyword)
            && window.Title.Contains(titleKeyword, StringComparison.OrdinalIgnoreCase);
        var allowedProcesses = ParseProcessNames(config.AutoPasteProcessNames);
        var processMatch = allowedProcesses.Contains(window.ProcessName.Trim());
        var classMatch = string.Equals(window.ClassName?.Trim(), "SunAwtDialog", StringComparison.OrdinalIgnoreCase);
        return titleMatch && processMatch && classMatch;
    }

    private static HashSet<string> ParseProcessNames(string? processNames)
    {
        return (processNames ?? string.Empty)
            .Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
