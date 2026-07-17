using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class KeyboardInputServiceTests
{
    [Fact]
    public void SendInput_struct_size_matches_windows_input_layout()
    {
        var expectedSize = IntPtr.Size == 8 ? 40 : 28;

        Assert.Equal(expectedSize, KeyboardInputService.SendInputStructSize);
    }

    [Fact]
    public async Task SendTextAndEnterIfFocusedAsync_sends_unicode_then_enter_with_10ms_delay()
    {
        var target = CreateDialog(10);
        var sentSteps = new List<string>();
        var delays = new List<TimeSpan>();
        var service = new KeyboardInputService(
            () => target,
            (shortcut, _, requested) =>
            {
                sentSteps.Add($"{shortcut}:{requested}");
                return (requested, 0);
            },
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var result = await service.SendTextAndEnterIfFocusedAsync(target, "A1!", CancellationToken.None);

        Assert.Equal(AdminUiAutoLoginStatus.InputSubmitted, result.Status);
        Assert.Equal(["UnicodeText:6", "Enter:2"], sentSteps);
        Assert.Equal([TimeSpan.FromMilliseconds(10)], delays);
    }

    [Fact]
    public async Task SendTextAndEnterIfFocusedAsync_does_not_type_when_target_is_not_foreground()
    {
        var target = CreateDialog(10);
        var other = new ForegroundWindowInfo { Handle = new IntPtr(20), Title = "Other", ProcessName = "chrome", ClassName = "Chrome_WidgetWin_1" };
        var sentSteps = new List<string>();
        var service = new KeyboardInputService(
            () => other,
            (shortcut, _, requested) =>
            {
                sentSteps.Add(shortcut);
                return (requested, 0);
            },
            (_, _) => Task.CompletedTask);

        var result = await service.SendTextAndEnterIfFocusedAsync(target, "secret", CancellationToken.None);

        Assert.Equal(AdminUiAutoLoginStatus.FocusLostBeforeInput, result.Status);
        Assert.Empty(sentSteps);
    }

    [Fact]
    public async Task SendTextAndEnterIfFocusedAsync_does_not_send_enter_after_focus_loss()
    {
        var target = CreateDialog(10);
        var other = new ForegroundWindowInfo { Handle = new IntPtr(20), Title = "Other", ProcessName = "chrome", ClassName = "Chrome_WidgetWin_1" };
        var windows = new Queue<ForegroundWindowInfo?>([target, other]);
        var sentSteps = new List<string>();
        var service = new KeyboardInputService(
            () => windows.Count > 0 ? windows.Dequeue() : other,
            (shortcut, _, requested) =>
            {
                sentSteps.Add(shortcut);
                return (requested, 0);
            },
            (_, _) => Task.CompletedTask);

        var result = await service.SendTextAndEnterIfFocusedAsync(target, "secret", CancellationToken.None);

        Assert.Equal(AdminUiAutoLoginStatus.FocusLostBeforeEnter, result.Status);
        Assert.Equal(["UnicodeText"], sentSteps);
    }

    [Fact]
    public void Source_does_not_force_focus_or_block_system_input()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath("VSLoader", "Models", "Services", "KeyboardInputService.cs"));

        Assert.DoesNotContain("SetForegroundWindow", code, StringComparison.Ordinal);
        Assert.DoesNotContain("BlockInput", code, StringComparison.Ordinal);
        Assert.DoesNotContain("CriticalInputOverlay", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", code, StringComparison.Ordinal);
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
