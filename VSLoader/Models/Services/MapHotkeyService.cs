using System.Windows.Input;
using VSLoader.Models;

namespace VSLoader.Services;

public static class MapHotkeyService
{
    public static SaveResult Validate(MapHotkeyConfig config)
    {
        if (!config.Enabled)
        {
            return SaveResult.Ok();
        }

        if (string.IsNullOrWhiteSpace(config.Key) || !TryParseKey(config.Key, out var key))
        {
            return SaveResult.Fail("地图快捷键无效：请选择一个按键。");
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.System or Key.None)
        {
            return SaveResult.Fail("地图快捷键无效：主键不能是 Ctrl、Alt 或 Shift。");
        }

        if (CountModifiers(config) < 1)
        {
            return SaveResult.Fail("地图快捷键至少需要一个修饰键和一个主键。");
        }

        if (config.Alt && key == Key.F4)
        {
            return SaveResult.Fail("地图快捷键可能与 Windows 系统快捷键 Alt + F4 冲突，请更换。");
        }

        if (config.Alt && key == Key.Space)
        {
            return SaveResult.Fail("地图快捷键可能与 Windows 系统快捷键 Alt + Space 冲突，请更换。");
        }

        if (config.Ctrl && config.Alt && key == Key.Delete)
        {
            return SaveResult.Fail("地图快捷键可能与 Windows 系统快捷键 Ctrl + Alt + Delete 冲突，请更换。");
        }

        return IsSupportedKey(key)
            ? SaveResult.Ok()
            : SaveResult.Fail("地图快捷键无效：仅支持 A-Z、0-9、F1-F12 和 Space。");
    }

    public static string Format(MapHotkeyConfig config)
    {
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.Key))
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

    public static bool IsMatch(MapHotkeyConfig config, Key key, ModifierKeys modifiers)
    {
        if (!config.Enabled || !TryParseKey(config.Key, out var configuredKey))
        {
            return false;
        }

        return key == configuredKey && modifiers == GetModifierKeys(config);
    }

    public static bool HasSameGestureAsMainHotkey(MapHotkeyConfig mapHotkey, HotkeyConfig mainHotkey)
    {
        if (!mapHotkey.Enabled
            || !mainHotkey.Enabled
            || !string.Equals(mainHotkey.InputType, "Keyboard", StringComparison.OrdinalIgnoreCase)
            || !TryParseKey(mapHotkey.Key, out var mapKey)
            || !TryParseKey(mainHotkey.Key, out var mainKey))
        {
            return false;
        }

        return mapKey == mainKey
            && mapHotkey.Ctrl == mainHotkey.Ctrl
            && mapHotkey.Alt == mainHotkey.Alt
            && mapHotkey.Shift == mainHotkey.Shift;
    }

    public static bool IsSupportedKey(Key key)
    {
        return key is >= Key.A and <= Key.Z
            || key is >= Key.D0 and <= Key.D9
            || key is >= Key.NumPad0 and <= Key.NumPad9
            || key is >= Key.F1 and <= Key.F12
            || key is Key.Space;
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

    private static string NormalizeKeyName(string key)
    {
        return TryParseKey(key, out var parsedKey) ? GetDisplayKey(parsedKey) : key.Trim();
    }

    internal static uint GetWin32Modifiers(MapHotkeyConfig config)
    {
        uint modifiers = 0;
        if (config.Alt)
        {
            modifiers |= 0x0001;
        }

        if (config.Ctrl)
        {
            modifiers |= 0x0002;
        }

        if (config.Shift)
        {
            modifiers |= 0x0004;
        }

        return modifiers;
    }

    internal static Key ParseKeyOrNone(string key)
    {
        return TryParseKey(key, out var parsedKey) ? parsedKey : Key.None;
    }

    private static ModifierKeys GetModifierKeys(MapHotkeyConfig config)
    {
        var modifiers = ModifierKeys.None;
        if (config.Ctrl)
        {
            modifiers |= ModifierKeys.Control;
        }

        if (config.Alt)
        {
            modifiers |= ModifierKeys.Alt;
        }

        if (config.Shift)
        {
            modifiers |= ModifierKeys.Shift;
        }

        return modifiers;
    }

    private static int CountModifiers(MapHotkeyConfig config)
    {
        return (config.Ctrl ? 1 : 0) + (config.Alt ? 1 : 0) + (config.Shift ? 1 : 0);
    }

    private static bool TryParseKey(string key, out Key parsedKey)
    {
        parsedKey = Key.None;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalized = key.Trim();
        if (normalized.Length == 1 && char.IsDigit(normalized[0]))
        {
            normalized = "D" + normalized;
        }

        return Enum.TryParse(normalized, true, out parsedKey);
    }
}
