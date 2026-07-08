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
    public void Paste_before_enter_delay_balances_speed_and_java_swing_clipboard_stability()
    {
        Assert.Equal(0, KeyboardInputService.PasteBeforeEnterDelayMilliseconds);
        Assert.Equal(120, KeyboardInputService.FocusSettleDelayMilliseconds);
    }

    [Fact]
    public void SendPasteAndEnter_does_not_paste_when_foreground_is_not_target_before_paste()
    {
        var target = CreateDialog(10);
        var other = new ForegroundWindowInfo
        {
            Handle = new IntPtr(20),
            Title = "Other",
            ProcessName = "chrome",
            ClassName = "Chrome_WidgetWin_1"
        };
        var sentShortcuts = new List<string>();
        var service = new KeyboardInputService(
            () => other,
            _ => true,
            (shortcut, _) =>
            {
                sentShortcuts.Add(shortcut);
                return (0, 0);
            },
            _ => { });

        var exception = Assert.Throws<InvalidOperationException>(() => service.SendPasteAndEnter(target));

        Assert.Contains("粘贴前", exception.Message);
        Assert.Empty(sentShortcuts);
    }

    [Fact]
    public void SendPasteAndEnter_refocuses_original_target_and_sends_enter_after_paste()
    {
        var target = CreateDialog(10);
        var other = new ForegroundWindowInfo
        {
            Handle = new IntPtr(20),
            Title = "Other",
            ProcessName = "chrome",
            ClassName = "Chrome_WidgetWin_1"
        };
        var foregroundQueue = new Queue<ForegroundWindowInfo?>([
            target,
            target
        ]);
        var focusCalls = new List<IntPtr>();
        var sentShortcuts = new List<string>();
        var service = new KeyboardInputService(
            () => foregroundQueue.Count > 0 ? foregroundQueue.Dequeue() : target,
            handle =>
            {
                focusCalls.Add(handle);
                return true;
            },
            (shortcut, requested) =>
            {
                sentShortcuts.Add(shortcut);
                return (requested, 0);
            },
            _ => { });

        service.SendPasteAndEnter(target);

        Assert.Contains(target.Handle, focusCalls);
        Assert.Equal(["Ctrl+V", "Enter"], sentShortcuts);
    }

    [Fact]
    public void SendPasteAndEnter_does_not_send_enter_after_paste_when_target_cannot_be_refocused()
    {
        var target = CreateDialog(10);
        var other = new ForegroundWindowInfo
        {
            Handle = new IntPtr(20),
            Title = "Other",
            ProcessName = "chrome",
            ClassName = "Chrome_WidgetWin_1"
        };
        var foregroundQueue = new Queue<ForegroundWindowInfo?>([
            target,
            other
        ]);
        var sentShortcuts = new List<string>();
        var focusCallCount = 0;
        var service = new KeyboardInputService(
            () => foregroundQueue.Count > 0 ? foregroundQueue.Dequeue() : other,
            _ => ++focusCallCount == 1,
            (shortcut, requested) =>
            {
                sentShortcuts.Add(shortcut);
                return (requested, 0);
            },
            _ => { });

        var exception = Assert.Throws<InvalidOperationException>(() => service.SendPasteAndEnter(target));

        Assert.Contains("Enter 前", exception.Message);
        Assert.Equal(["Ctrl+V"], sentShortcuts);
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
