using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VSLoader.Services;

public sealed class KeyboardInputService
{
    private const int InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const ushort VirtualKeyEnter = 0x0D;
    internal const int TextBeforeEnterDelayMilliseconds = 10;

    private readonly Func<ForegroundWindowInfo?> getForegroundWindowInfo;
    private readonly Func<string, string?, uint, (uint SentInputCount, int NativeErrorCode)> sendInput;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;

    internal static int SendInputStructSize => Marshal.SizeOf<Input>();

    public KeyboardInputService()
        : this(
            () => new ForegroundWindowService().GetForegroundWindowInfo(),
            SendShortcutWithSendInput,
            Task.Delay)
    {
    }

    internal KeyboardInputService(
        Func<ForegroundWindowInfo?> getForegroundWindowInfo,
        Func<string, string?, uint, (uint SentInputCount, int NativeErrorCode)> sendInput,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        this.getForegroundWindowInfo = getForegroundWindowInfo;
        this.sendInput = sendInput;
        this.delayAsync = delayAsync;
    }

    public async Task<AdminUiAutoPasteResult> SendTextAndEnterIfFocusedAsync(
        ForegroundWindowInfo targetWindow,
        string text,
        CancellationToken cancellationToken,
        AdminUiAutoPasteLogService? logService = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return AdminUiAutoPasteResult.PasswordEmpty();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var actualBeforeInput = getForegroundWindowInfo();
        if (!IsSameForegroundTarget(targetWindow, actualBeforeInput))
        {
            logService?.LogFocusLost("BeforeInput", targetWindow, actualBeforeInput);
            return AdminUiAutoPasteResult.FocusLostBeforeInput(targetWindow);
        }

        logService?.LogInputStart(targetWindow, text.Length);
        try
        {
            SendTextSequence(text, logService);
            await delayAsync(TimeSpan.FromMilliseconds(TextBeforeEnterDelayMilliseconds), cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            var actualBeforeEnter = getForegroundWindowInfo();
            if (!IsSameForegroundTarget(targetWindow, actualBeforeEnter))
            {
                logService?.LogFocusLost("BeforeEnter", targetWindow, actualBeforeEnter);
                return AdminUiAutoPasteResult.FocusLostBeforeEnter(targetWindow);
            }

            SendEnter(logService);
            return AdminUiAutoPasteResult.InputSubmitted(targetWindow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logService?.LogError(ex);
            return AdminUiAutoPasteResult.InputFailed($"自动填写失败：{ex.Message}", targetWindow);
        }
    }

    private void SendTextSequence(string text, AdminUiAutoPasteLogService? logService)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestedInputCount = checked((uint)(text.Length * 2));
        var result = sendInput("UnicodeText", text, requestedInputCount);
        stopwatch.Stop();
        logService?.LogTextSent(requestedInputCount, result.SentInputCount, stopwatch.ElapsedMilliseconds, result.NativeErrorCode);
        if (result.SentInputCount != requestedInputCount)
        {
            throw new Win32Exception(result.NativeErrorCode, "发送密码文本输入失败。");
        }
    }

    private void SendEnter(AdminUiAutoPasteLogService? logService)
    {
        const uint requestedInputCount = 2;
        var stopwatch = Stopwatch.StartNew();
        var result = sendInput("Enter", null, requestedInputCount);
        stopwatch.Stop();
        logService?.LogEnterSent(requestedInputCount, result.SentInputCount, stopwatch.ElapsedMilliseconds, result.NativeErrorCode);
        if (result.SentInputCount != requestedInputCount)
        {
            throw new Win32Exception(result.NativeErrorCode, "发送确认键失败。");
        }
    }

    private static bool IsSameForegroundTarget(ForegroundWindowInfo targetWindow, ForegroundWindowInfo? actualWindow)
    {
        return targetWindow.Handle != IntPtr.Zero && actualWindow?.Handle == targetWindow.Handle;
    }

    private static (uint SentInputCount, int NativeErrorCode) SendShortcutWithSendInput(
        string shortcut,
        string? text,
        uint requestedInputCount)
    {
        var inputs = string.Equals(shortcut, "UnicodeText", StringComparison.OrdinalIgnoreCase)
            ? BuildUnicodeTextInputs(text ?? string.Empty)
            : [KeyDown(VirtualKeyEnter), KeyUp(VirtualKeyEnter)];

        var sent = SendInput(requestedInputCount, inputs, SendInputStructSize);
        return (sent, sent == requestedInputCount ? 0 : Marshal.GetLastWin32Error());
    }

    private static Input[] BuildUnicodeTextInputs(string text)
    {
        var inputs = new Input[checked(text.Length * 2)];
        var index = 0;
        foreach (var character in text)
        {
            inputs[index++] = UnicodeKey(character, keyUp: false);
            inputs[index++] = UnicodeKey(character, keyUp: true);
        }

        return inputs;
    }

    private static Input KeyDown(ushort virtualKey)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey } }
        };
    }

    private static Input KeyUp(ushort virtualKey)
    {
        var input = KeyDown(virtualKey);
        input.Data.Keyboard.Flags = KeyEventKeyUp;
        return input;
    }

    private static Input UnicodeKey(char character, bool keyUp)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    Scan = character,
                    Flags = keyUp ? KeyEventUnicode | KeyEventKeyUp : KeyEventUnicode
                }
            }
        };
    }

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
