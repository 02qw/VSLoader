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
        Assert.Equal(80, KeyboardInputService.PasteBeforeEnterDelayMilliseconds);
        Assert.Equal(80, KeyboardInputService.FocusSettleDelayMilliseconds);
        Assert.Equal(1500, KeyboardInputService.ForceFocusRetryTimeoutMilliseconds);
        Assert.Equal(40, KeyboardInputService.ForceFocusRetryIntervalMilliseconds);
        Assert.Equal(3, KeyboardInputService.CriticalInputFocusMaxAttempts);
    }

    [Fact]
    public void SendPasteAndEnter_retries_focus_before_paste_when_user_switches_away()
    {
        var target = CreateDialog(10);
        var other = new ForegroundWindowInfo
        {
            Handle = new IntPtr(20),
            Title = "Other",
            ProcessName = "chrome",
            ClassName = "Chrome_WidgetWin_1"
        };
        var focusCallCount = 0;
        var sentShortcuts = new List<string>();
        var service = new KeyboardInputService(
            () => focusCallCount >= 2 ? target : other,
            handle =>
            {
                Assert.Equal(target.Handle, handle);
                focusCallCount++;
                return true;
            },
            (shortcut, requested) =>
            {
                sentShortcuts.Add(shortcut);
                return (requested, 0);
            },
            _ => { });

        service.SendPasteAndEnter(target);

        Assert.True(focusCallCount >= 2);
        Assert.Equal(["Ctrl+V", "Enter"], sentShortcuts);
    }

    [Fact]
    public void SendPasteAndEnter_does_not_paste_when_target_cannot_be_refocused_before_paste()
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
            _ => false,
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
    public void SendPasteAndEnter_blocks_user_input_only_for_critical_key_sequence()
    {
        var target = CreateDialog(10);
        var foregroundQueue = new Queue<ForegroundWindowInfo?>([
            target,
            target,
            target,
            target
        ]);
        var events = new List<string>();
        var service = new KeyboardInputService(
            () => foregroundQueue.Count > 0 ? foregroundQueue.Dequeue() : target,
            _ =>
            {
                events.Add("focus");
                return true;
            },
            (shortcut, requested) =>
            {
                events.Add(shortcut);
                return (requested, 0);
            },
            _ => { },
            blockInput: block =>
            {
                events.Add(block ? "block-on" : "block-off");
                return (true, 0);
            });

        service.SendPasteAndEnter(target);

        Assert.Contains("block-on", events);
        Assert.Contains("block-off", events);
        Assert.True(events.IndexOf("block-on") < events.IndexOf("Ctrl+V"));
        Assert.True(events.IndexOf("Enter") < events.IndexOf("block-off"));
    }

    [Fact]
    public void SendPasteAndEnter_uses_overlay_fallback_when_input_lock_fails()
    {
        var target = CreateDialog(10);
        var foregroundQueue = new Queue<ForegroundWindowInfo?>([
            target,
            target,
            target,
            target
        ]);
        var sentShortcuts = new List<string>();
        var overlay = new TestCriticalInputOverlayScope(isActive: true);
        var service = new KeyboardInputService(
            () => foregroundQueue.Count > 0 ? foregroundQueue.Dequeue() : target,
            _ => true,
            (shortcut, requested) =>
            {
                sentShortcuts.Add(shortcut);
                return (requested, 0);
            },
            _ => { },
            blockInput: _ => (false, 5),
            showOverlay: () => overlay);

        service.SendPasteAndEnter(target);

        Assert.Equal(["Ctrl+V", "Enter"], sentShortcuts);
        Assert.True(overlay.Disposed);
    }

    [Fact]
    public void SendPasteAndEnter_does_not_paste_when_input_lock_and_overlay_both_fail()
    {
        var target = CreateDialog(10);
        var sentShortcuts = new List<string>();
        var service = new KeyboardInputService(
            () => target,
            _ => true,
            (shortcut, requested) =>
            {
                sentShortcuts.Add(shortcut);
                return (requested, 0);
            },
            _ => { },
            blockInput: _ => (false, 5),
            showOverlay: () => new TestCriticalInputOverlayScope(isActive: false));

        var exception = Assert.Throws<InvalidOperationException>(() => service.SendPasteAndEnter(target));

        Assert.Contains("关键输入阶段保护失败", exception.Message);
        Assert.Empty(sentShortcuts);
    }

    [Fact]
    public void SendPasteAndEnter_releases_overlay_when_enter_send_fails()
    {
        var target = CreateDialog(10);
        var foregroundQueue = new Queue<ForegroundWindowInfo?>([
            target,
            target,
            target,
            target
        ]);
        var overlay = new TestCriticalInputOverlayScope(isActive: true);
        var service = new KeyboardInputService(
            () => foregroundQueue.Count > 0 ? foregroundQueue.Dequeue() : target,
            _ => true,
            (shortcut, requested) => shortcut == "Enter" ? (0, 5) : (requested, 0),
            _ => { },
            blockInput: _ => (false, 5),
            showOverlay: () => overlay);

        Assert.Throws<System.ComponentModel.Win32Exception>(() => service.SendPasteAndEnter(target));

        Assert.True(overlay.Disposed);
    }

    [Fact]
    public void SendPasteAndEnter_releases_input_lock_when_enter_send_fails()
    {
        var target = CreateDialog(10);
        var foregroundQueue = new Queue<ForegroundWindowInfo?>([
            target,
            target,
            target,
            target
        ]);
        var blockStates = new List<bool>();
        var service = new KeyboardInputService(
            () => foregroundQueue.Count > 0 ? foregroundQueue.Dequeue() : target,
            _ => true,
            (shortcut, requested) => shortcut == "Enter" ? (0, 5) : (requested, 0),
            _ => { },
            blockInput: block =>
            {
                blockStates.Add(block);
                return (true, 0);
            });

        Assert.Throws<System.ComponentModel.Win32Exception>(() => service.SendPasteAndEnter(target));

        Assert.Equal([true, false], blockStates);
    }

    [Fact]
    public void SendPasteAndEnter_retries_focus_before_enter_when_user_switches_away_after_paste()
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
        var focusCallCount = 0;
        var service = new KeyboardInputService(
            () =>
            {
                if (focusCallCount <= 1)
                {
                    return target;
                }

                return focusCallCount >= 3 ? target : other;
            },
            handle =>
            {
                Assert.Equal(target.Handle, handle);
                focusCallCount++;
                return true;
            },
            (shortcut, requested) =>
            {
                sentShortcuts.Add(shortcut);
                return (requested, 0);
            },
            _ => { });

        service.SendPasteAndEnter(target);

        Assert.True(focusCallCount >= 3);
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
        var sentShortcuts = new List<string>();
        var focusCallCount = 0;
        var service = new KeyboardInputService(
            () => ++focusCallCount <= 2 ? target : other,
            _ => true,
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

    private sealed class TestCriticalInputOverlayScope(bool isActive) : ICriticalInputOverlayScope
    {
        public bool IsActive { get; } = isActive;

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
