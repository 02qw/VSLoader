using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int MainHotkeyId = 0x5653;
    private const int MapHotkeyId = 0x4D50;
    private const int WmHotkey = 0x0312;
    private const int WhMouseLl = 14;
    private const int WmXbuttondown = 0x020B;
    private const int Xbutton1 = 0x0001;
    private const int Xbutton2 = 0x0002;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;

    private HwndSource? _source;
    private IntPtr _windowHandle;
    private IntPtr _mouseHook;
    private LowLevelMouseProc? _mouseProc;
    private HotkeyConfig? _registeredMouseHotkey;
    private Action? _hotkeyPressed;
    private Action? _mapHotkeyPressed;
    private bool _registered;
    private bool _mapRegistered;

    public void Initialize(Window window, Action hotkeyPressed)
    {
        Initialize(window);
        _hotkeyPressed = hotkeyPressed;
    }

    public void Initialize(Window window)
    {
        _windowHandle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WndProc);
    }

    public SaveResult Register(HotkeyConfig config)
    {
        Unregister();

        if (!config.Enabled)
        {
            return SaveResult.Ok();
        }

        var validation = Validate(config);
        if (!validation.Success)
        {
            return validation;
        }

        if (_windowHandle == IntPtr.Zero)
        {
            return SaveResult.Fail("主窗口尚未初始化，无法注册快捷键。");
        }

        if (IsMouseHotkey(config))
        {
            return RegisterMouseHotkey(config);
        }

        var modifiers = GetModifiers(config);
        var virtualKey = KeyInterop.VirtualKeyFromKey(ParseKey(config.Key));
        if (!RegisterHotKey(_windowHandle, MainHotkeyId, modifiers, (uint)virtualKey))
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return SaveResult.Fail($"快捷键注册失败，可能已被系统或其他程序占用，请更换快捷键。\n\n错误原因：{error}");
        }

        _registered = true;
        return SaveResult.Ok();
    }

    public SaveResult RegisterMapHotkey(MapHotkeyConfig config, Action hotkeyPressed)
    {
        UnregisterMapHotkey();
        _mapHotkeyPressed = hotkeyPressed;

        if (!config.Enabled)
        {
            return SaveResult.Ok();
        }

        var validation = MapHotkeyService.Validate(config);
        if (!validation.Success)
        {
            return validation;
        }

        if (_windowHandle == IntPtr.Zero)
        {
            return SaveResult.Fail("主窗口尚未初始化，无法注册地图快捷键。");
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(MapHotkeyService.ParseKeyOrNone(config.Key));
        if (!RegisterHotKey(_windowHandle, MapHotkeyId, MapHotkeyService.GetWin32Modifiers(config), (uint)virtualKey))
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return SaveResult.Fail($"地图快捷键注册失败，可能已被系统或其他程序占用，请更换快捷键。\n\n错误原因：{error}");
        }

        _mapRegistered = true;
        return SaveResult.Ok();
    }

    public void Unregister()
    {
        if (_registered && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, MainHotkeyId);
            _registered = false;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
            _registeredMouseHotkey = null;
        }
    }

    public void UnregisterMapHotkey()
    {
        if (_mapRegistered && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, MapHotkeyId);
            _mapRegistered = false;
        }
    }

    public static SaveResult Validate(HotkeyConfig config)
    {
        if (!config.Enabled)
        {
            return SaveResult.Ok();
        }

        if (IsMouseHotkey(config))
        {
            return ValidateMouse(config);
        }

        if (string.IsNullOrWhiteSpace(config.Key) || !TryParseKey(config.Key, out var key))
        {
            return SaveResult.Fail("快捷键无效：请选择一个主键。");
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.System or Key.None)
        {
            return SaveResult.Fail("快捷键无效：主键不能是 Ctrl、Alt 或 Shift。");
        }

        var modifierCount = CountModifiers(config);
        if (modifierCount < 1)
        {
            return SaveResult.Fail("快捷键至少需要一个修饰键和一个主键。");
        }

        if (config.Alt && key == Key.F4)
        {
            return SaveResult.Fail("快捷键可能与 Windows 系统快捷键 Alt + F4 冲突，请更换。");
        }

        if (config.Alt && key == Key.Space)
        {
            return SaveResult.Fail("快捷键可能与 Windows 系统快捷键 Alt + Space 冲突，请更换。");
        }

        if (config.Ctrl && config.Alt && key == Key.Delete)
        {
            return SaveResult.Fail("快捷键可能与 Windows 系统快捷键 Ctrl + Alt + Delete 冲突，请更换。");
        }

        if (!IsSupportedMainKey(key))
        {
            return SaveResult.Fail("快捷键无效：第一版仅支持 A-Z、0-9、F1-F12 和 Space。");
        }

        return SaveResult.Ok();
    }

    public static string Format(HotkeyConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Key))
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (config.Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (config.Alt)
        {
            parts.Add("Alt");
        }

        if (config.Shift)
        {
            parts.Add("Shift");
        }

        parts.Add(NormalizeKeyName(config.Key));
        return string.Join(" + ", parts);
    }

    public void Dispose()
    {
        Unregister();
        UnregisterMapHotkey();
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == MainHotkeyId)
        {
            _hotkeyPressed?.Invoke();
            handled = true;
        }
        else if (msg == WmHotkey && wParam.ToInt32() == MapHotkeyId)
        {
            _mapHotkeyPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private SaveResult RegisterMouseHotkey(HotkeyConfig config)
    {
        _mouseProc = MouseHookProc;
        _registeredMouseHotkey = config.Clone();
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, IntPtr.Zero, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            _registeredMouseHotkey = null;
            var error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return SaveResult.Fail($"鼠标侧键快捷键注册失败，请稍后重试。\n\n错误原因：{error}");
        }

        return SaveResult.Ok();
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == WmXbuttondown && _registeredMouseHotkey is not null)
        {
            var hookStruct = Marshal.PtrToStructure<Msllhookstruct>(lParam);
            var xButton = (hookStruct.MouseData >> 16) & 0xffff;
            var key = xButton switch
            {
                Xbutton1 => "Mouse4",
                Xbutton2 => "Mouse5",
                _ => string.Empty
            };

            if (string.Equals(key, _registeredMouseHotkey.Key, StringComparison.OrdinalIgnoreCase)
                && IsModifierPressed(VkControl) == _registeredMouseHotkey.Ctrl
                && IsModifierPressed(VkMenu) == _registeredMouseHotkey.Alt
                && IsModifierPressed(VkShift) == _registeredMouseHotkey.Shift)
            {
                _hotkeyPressed?.Invoke();
                return new IntPtr(1);
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static uint GetModifiers(HotkeyConfig config)
    {
        uint modifiers = 0;
        if (config.Ctrl)
        {
            modifiers |= ModControl;
        }

        if (config.Alt)
        {
            modifiers |= ModAlt;
        }

        if (config.Shift)
        {
            modifiers |= ModShift;
        }

        return modifiers;
    }

    private static int CountModifiers(HotkeyConfig config)
    {
        return (config.Ctrl ? 1 : 0) + (config.Alt ? 1 : 0) + (config.Shift ? 1 : 0);
    }

    private static SaveResult ValidateMouse(HotkeyConfig config)
    {
        if (!IsMouseButtonKey(config.Key))
        {
            return SaveResult.Fail("快捷键无效：鼠标快捷键仅支持 Mouse4 或 Mouse5。");
        }

        if (CountModifiers(config) < 1)
        {
            return SaveResult.Fail("鼠标侧键快捷键至少需要一个修饰键。");
        }

        return SaveResult.Ok();
    }

    private static bool IsMouseHotkey(HotkeyConfig config)
    {
        return string.Equals(config.InputType, "Mouse", StringComparison.OrdinalIgnoreCase)
            || IsMouseButtonKey(config.Key);
    }

    private static bool IsMouseButtonKey(string key)
    {
        return string.Equals(key, "Mouse4", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Mouse5", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsModifierPressed(int virtualKey)
    {
        return (GetKeyState(virtualKey) & 0x8000) != 0;
    }

    private static bool IsSupportedMainKey(Key key)
    {
        return key is >= Key.A and <= Key.Z
            || key is >= Key.D0 and <= Key.D9
            || key is >= Key.NumPad0 and <= Key.NumPad9
            || key is >= Key.F1 and <= Key.F12
            || key == Key.Space;
    }

    private static Key ParseKey(string key)
    {
        return TryParseKey(key, out var parsedKey) ? parsedKey : Key.None;
    }

    private static bool TryParseKey(string key, out Key parsedKey)
    {
        return Enum.TryParse(key, true, out parsedKey);
    }

    private static string NormalizeKeyName(string key)
    {
        if (IsMouseButtonKey(key))
        {
            return string.Equals(key, "Mouse5", StringComparison.OrdinalIgnoreCase) ? "Mouse5" : "Mouse4";
        }

        return TryParseKey(key, out var parsedKey) ? GetDisplayKey(parsedKey) : key.Trim();
    }

    public static string GetDisplayKey(Key key)
    {
        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString();
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return ((int)(key - Key.NumPad0)).ToString();
        }

        return key == Key.Space ? "Space" : key.ToString();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msllhookstruct
    {
        public Point Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);
}
