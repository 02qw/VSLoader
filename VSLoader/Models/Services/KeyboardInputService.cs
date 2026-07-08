using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VSLoader.Services;

public sealed class KeyboardInputService
{
    private const int InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyV = 0x56;
    private const ushort VirtualKeyEnter = 0x0D;
    internal const int FocusSettleDelayMilliseconds = 80;
    private static readonly TimeSpan FocusSettleDelay = TimeSpan.FromMilliseconds(FocusSettleDelayMilliseconds);
    internal const int ForceFocusRetryTimeoutMilliseconds = 1500;
    internal const int ForceFocusRetryIntervalMilliseconds = 40;
    internal const int CriticalInputFocusMaxAttempts = 3;
    private static readonly TimeSpan ForceFocusRetryInterval = TimeSpan.FromMilliseconds(ForceFocusRetryIntervalMilliseconds);
    internal const int PasteBeforeEnterDelayMilliseconds = 80;
    private static readonly TimeSpan PasteBeforeEnterDelay = TimeSpan.FromMilliseconds(PasteBeforeEnterDelayMilliseconds);
    private readonly Func<ForegroundWindowInfo?> getForegroundWindowInfo;
    private readonly Func<IntPtr, bool> setForegroundWindow;
    private readonly Func<string, uint, (uint SentInputCount, int NativeErrorCode)> sendShortcut;
    private readonly Action<TimeSpan> sleep;
    private readonly Func<bool, (bool Success, int NativeErrorCode)> blockInput;
    private readonly Func<ICriticalInputOverlayScope> showOverlay;

    internal static int SendInputStructSize => Marshal.SizeOf<Input>();

    public KeyboardInputService()
        : this(
            () => new ForegroundWindowService().GetForegroundWindowInfo(),
            SetForegroundWindow,
            SendShortcutWithSendInput,
            Thread.Sleep,
            block => (BlockInput(block), Marshal.GetLastWin32Error()),
            new CriticalInputOverlayService().Show)
    {
    }

    internal KeyboardInputService(
        Func<ForegroundWindowInfo?> getForegroundWindowInfo,
        Func<IntPtr, bool> setForegroundWindow,
        Func<string, uint, (uint SentInputCount, int NativeErrorCode)> sendShortcut,
        Action<TimeSpan> sleep,
        Func<bool, (bool Success, int NativeErrorCode)>? blockInput = null,
        Func<ICriticalInputOverlayScope>? showOverlay = null)
    {
        this.getForegroundWindowInfo = getForegroundWindowInfo;
        this.setForegroundWindow = setForegroundWindow;
        this.sendShortcut = sendShortcut;
        this.sleep = sleep;
        this.blockInput = blockInput ?? (_ => (true, 0));
        this.showOverlay = showOverlay ?? (() => CriticalInputOverlayService.ShowInactiveScope());
    }

    public void SendPasteAndEnter()
    {
        SendPasteAndEnter(new ForegroundWindowInfo());
    }

    public void SendPasteAndEnter(ForegroundWindowInfo targetWindow, AdminUiAutoPasteLogService? logService = null)
    {
        logService?.LogStage(AdminUiAutoPasteStage.BeforePaste, targetWindow);
        logService?.LogKeyboardPlan(targetWindow, FocusSettleDelayMilliseconds, PasteBeforeEnterDelayMilliseconds, SendInputStructSize);
        EnsureTargetForeground(targetWindow, AdminUiAutoPasteStage.BeforePaste, "粘贴前", logService);
        ExecuteWithInputBlocked(targetWindow, logService, () =>
        {
            EnsureTargetForeground(targetWindow, AdminUiAutoPasteStage.BeforePaste, "粘贴前", logService, CriticalInputFocusMaxAttempts);
            SendKeySequence("Ctrl+V", logService);
            logService?.LogStage(AdminUiAutoPasteStage.PasteSent, targetWindow);
            logService?.LogKeyboardDelay("AfterPasteBeforeEnter", PasteBeforeEnterDelayMilliseconds);
            sleep(PasteBeforeEnterDelay);
            logService?.LogStage(AdminUiAutoPasteStage.BeforeEnter, targetWindow);
            EnsureTargetForeground(targetWindow, AdminUiAutoPasteStage.BeforeEnter, "Enter 前", logService, CriticalInputFocusMaxAttempts);
            SendKeySequence("Enter", logService);
            logService?.LogStage(AdminUiAutoPasteStage.EnterSent, targetWindow);
            logService?.LogStage(AdminUiAutoPasteStage.Completed, targetWindow);
        });
    }

    private void ExecuteWithInputBlocked(ForegroundWindowInfo targetWindow, AdminUiAutoPasteLogService? logService, Action action)
    {
        var locked = false;
        try
        {
            var lockResult = blockInput(true);
            locked = lockResult.Success;
            logService?.LogInputBlock(targetWindow, requestedBlock: true, success: locked, lockResult.NativeErrorCode);
            if (locked)
            {
                action();
                return;
            }

            logService?.LogInputProtection(targetWindow, "BlockInput", success: false, lockResult.NativeErrorCode);
            using var overlay = showOverlay();
            logService?.LogInputProtection(targetWindow, "Overlay", overlay.IsActive);
            if (!overlay.IsActive)
            {
                throw new InvalidOperationException($"关键输入阶段保护失败，未发送自动粘贴按键。BlockInput nativeErrorCode={lockResult.NativeErrorCode}");
            }

            action();
        }
        finally
        {
            if (locked)
            {
                var releaseResult = blockInput(false);
                logService?.LogInputBlock(targetWindow, requestedBlock: false, success: releaseResult.Success, releaseResult.NativeErrorCode);
            }
        }
    }

    private void EnsureTargetForeground(
        ForegroundWindowInfo targetWindow,
        AdminUiAutoPasteStage stage,
        string failureStageName,
        AdminUiAutoPasteLogService? logService,
        int? maxAttemptsOverride = null)
    {
        if (targetWindow.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"{failureStageName}无法确认 AdminUI 登录窗口。");
        }

        var totalStopwatch = Stopwatch.StartNew();
        var maxAttempts = maxAttemptsOverride ?? Math.Max(1, (int)Math.Ceiling(
            ForceFocusRetryTimeoutMilliseconds / (double)(FocusSettleDelayMilliseconds + ForceFocusRetryIntervalMilliseconds)));

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            logService?.LogKeyboardForeground($"{stage}:BeforeFocus", getForegroundWindowInfo());
            var focusStopwatch = Stopwatch.StartNew();
            var focusResult = setForegroundWindow(targetWindow.Handle);
            focusStopwatch.Stop();
            logService?.LogKeyboardStep("SetForegroundWindow", "FocusTarget", 1, focusResult ? 1u : 0u, focusStopwatch.ElapsedMilliseconds, Marshal.GetLastWin32Error());
            logService?.LogFocusRetry(stage, targetWindow, attempt, focusResult);
            sleep(FocusSettleDelay);

            var actualWindow = getForegroundWindowInfo();
            logService?.LogKeyboardForeground($"{stage}:AfterFocus", actualWindow);
            var matched = IsSameForegroundTarget(targetWindow, actualWindow);
            logService?.LogFocusCheck(stage, targetWindow, actualWindow, matched);
            if (matched)
            {
                totalStopwatch.Stop();
                logService?.LogFocusRetryResult(stage, targetWindow, success: true, attempt, totalStopwatch.ElapsedMilliseconds);
                return;
            }

            if (attempt < maxAttempts)
            {
                sleep(ForceFocusRetryInterval);
            }
        }

        totalStopwatch.Stop();
        logService?.LogFocusRetryResult(stage, targetWindow, success: false, maxAttempts, totalStopwatch.ElapsedMilliseconds);
        logService?.LogStage(AdminUiAutoPasteStage.Aborted, targetWindow, $"{failureStageName}无法拉回目标 AdminUI 登录窗口。");
        var suffix = stage == AdminUiAutoPasteStage.BeforeEnter
            ? "密码可能已粘贴，请手动确认。"
            : string.Empty;
        throw new InvalidOperationException($"{failureStageName}无法拉回目标 AdminUI 登录窗口。{suffix}");
    }

    private void SendKeySequence(string shortcut, AdminUiAutoPasteLogService? logService)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestedInputCount = GetShortcutInputCount(shortcut);
        var result = sendShortcut(shortcut, requestedInputCount);
        stopwatch.Stop();
        logService?.LogKeyboardStep("SendInput", shortcut, requestedInputCount, result.SentInputCount, stopwatch.ElapsedMilliseconds, result.NativeErrorCode);
        if (result.SentInputCount != requestedInputCount)
        {
            throw new Win32Exception(result.NativeErrorCode, "发送键盘输入失败。");
        }
    }

    private static bool IsSameForegroundTarget(ForegroundWindowInfo targetWindow, ForegroundWindowInfo? actualWindow)
    {
        if (actualWindow is null || targetWindow.Handle == IntPtr.Zero)
        {
            return false;
        }

        return actualWindow.Handle == targetWindow.Handle;
    }

    private static uint GetShortcutInputCount(string shortcut)
    {
        return string.Equals(shortcut, "Ctrl+V", StringComparison.OrdinalIgnoreCase) ? 4u : 2u;
    }

    private static (uint SentInputCount, int NativeErrorCode) SendShortcutWithSendInput(string shortcut, uint requestedInputCount)
    {
        var inputs = string.Equals(shortcut, "Ctrl+V", StringComparison.OrdinalIgnoreCase)
            ? new[]
            {
                KeyDown(VirtualKeyControl),
                KeyDown(VirtualKeyV),
                KeyUp(VirtualKeyV),
                KeyUp(VirtualKeyControl)
            }
            : new[]
            {
                KeyDown(VirtualKeyEnter),
                KeyUp(VirtualKeyEnter)
            };

        var sent = SendInput(requestedInputCount, inputs, SendInputStructSize);
        var nativeErrorCode = sent == requestedInputCount ? 0 : Marshal.GetLastWin32Error();
        return (sent, nativeErrorCode);
    }

    private static Input KeyDown(ushort virtualKey)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey
                }
            }
        };
    }

    private static Input KeyUp(ushort virtualKey)
    {
        var input = KeyDown(virtualKey);
        input.Data.Keyboard.Flags = KeyEventKeyUp;
        return input;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool BlockInput(bool fBlockIt);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParamL;
        public ushort ParamH;
    }
}
