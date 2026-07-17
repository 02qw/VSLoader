using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class AdminUiAutoPasteServiceTests
{
    [Fact]
    public void IsAdminUiDialogWindow_requires_strict_foreground_dialog_identity()
    {
        var config = new AdminUiConfig();

        Assert.True(AdminUiAutoPasteService.IsAdminUiDialogWindow(CreateDialog(1), config));
        Assert.False(AdminUiAutoPasteService.IsAdminUiDialogWindow(new ForegroundWindowInfo
        {
            Handle = new IntPtr(2),
            Title = "TAOI008.processor",
            ProcessName = "javaw",
            ClassName = "SunAwtFrame"
        }, config));
        Assert.False(AdminUiAutoPasteService.IsAdminUiDialogWindow(new ForegroundWindowInfo
        {
            Handle = new IntPtr(3),
            Title = "Other",
            ProcessName = "javaw",
            ClassName = "SunAwtDialog"
        }, config));
        Assert.False(AdminUiAutoPasteService.IsAdminUiDialogWindow(new ForegroundWindowInfo
        {
            Handle = new IntPtr(4),
            Title = "TAOI008.processor",
            ProcessName = "chrome",
            ClassName = "SunAwtDialog"
        }, config));
    }

    [Fact]
    public async Task TryAutoLoginAsync_checks_only_foreground_window_and_submits_when_dialog_is_stable()
    {
        var dialog = CreateDialog(9);
        var foregroundWindows = new Queue<ForegroundWindowInfo?>([
            new ForegroundWindowInfo { Handle = new IntPtr(1), Title = "VSCode", ProcessName = "Code", ClassName = "Chrome_WidgetWin_1" },
            dialog,
            dialog
        ]);
        ForegroundWindowInfo? submittedWindow = null;
        string? submittedPassword = null;
        var now = DateTimeOffset.UtcNow;
        var service = new AdminUiAutoPasteService(
            () => foregroundWindows.Count > 0 ? foregroundWindows.Dequeue() : dialog,
            (window, password, _) =>
            {
                submittedWindow = window;
                submittedPassword = password;
                return Task.FromResult(AdminUiAutoPasteResult.InputSubmitted(window));
            },
            (delay, _) =>
            {
                now += delay;
                return Task.CompletedTask;
            },
            () => now);

        var result = await service.TryAutoLoginAsync(new AdminUiConfig(), "A1!");

        Assert.Equal(AdminUiAutoLoginStatus.InputSubmitted, result.Status);
        Assert.Same(dialog, submittedWindow);
        Assert.Equal("A1!", submittedPassword);
    }

    [Fact]
    public async Task TryAutoLoginAsync_aborts_when_dialog_loses_focus_during_stability_check()
    {
        var dialog = CreateDialog(9);
        var other = new ForegroundWindowInfo { Handle = new IntPtr(2), Title = "Other", ProcessName = "chrome", ClassName = "Chrome_WidgetWin_1" };
        var foregroundWindows = new Queue<ForegroundWindowInfo?>([dialog, other]);
        var sendCount = 0;
        var service = new AdminUiAutoPasteService(
            () => foregroundWindows.Count > 0 ? foregroundWindows.Dequeue() : other,
            (_, _, _) =>
            {
                sendCount++;
                return Task.FromResult(AdminUiAutoPasteResult.InputSubmitted(dialog));
            },
            (_, _) => Task.CompletedTask);

        var result = await service.TryAutoLoginAsync(new AdminUiConfig(), "secret");

        Assert.Equal(AdminUiAutoLoginStatus.FocusLostBeforeInput, result.Status);
        Assert.Equal(0, sendCount);
    }

    [Fact]
    public async Task TryAutoLoginAsync_times_out_without_enumerating_background_windows()
    {
        var now = DateTimeOffset.UtcNow;
        var foregroundReadCount = 0;
        var service = new AdminUiAutoPasteService(
            () =>
            {
                foregroundReadCount++;
                return new ForegroundWindowInfo { Handle = new IntPtr(1), Title = "Browser", ProcessName = "chrome", ClassName = "Chrome_WidgetWin_1" };
            },
            (_, _, _) => throw new InvalidOperationException("input should not run"),
            (delay, _) =>
            {
                now += delay;
                return Task.CompletedTask;
            },
            () => now);

        var result = await service.TryAutoLoginAsync(new AdminUiConfig { AutoPasteTimeoutSeconds = 1 }, "secret");

        Assert.Equal(AdminUiAutoLoginStatus.TimedOut, result.Status);
        Assert.InRange(foregroundReadCount, 9, 11);
    }

    [Fact]
    public async Task TryAutoLoginAsync_rejects_empty_password()
    {
        var service = new AdminUiAutoPasteService(
            () => CreateDialog(1),
            (_, _, _) => throw new InvalidOperationException("input should not run"),
            (_, _) => Task.CompletedTask);

        var result = await service.TryAutoLoginAsync(new AdminUiConfig(), string.Empty);

        Assert.Equal(AdminUiAutoLoginStatus.PasswordEmpty, result.Status);
    }

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
