using System.Windows.Input;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class MapHotkeyServiceTests
{
    [Fact]
    public void Validate_accepts_default_alt_x()
    {
        var config = new MapHotkeyConfig();
        var result = MapHotkeyService.Validate(config);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Alt + X", MapHotkeyService.Format(config));
    }

    [Fact]
    public void IsMatch_matches_configured_modifier_key_combination()
    {
        var config = new MapHotkeyConfig { Enabled = true, Alt = true, Key = "X" };

        Assert.True(MapHotkeyService.IsMatch(config, Key.X, ModifierKeys.Alt));
        Assert.False(MapHotkeyService.IsMatch(config, Key.X, ModifierKeys.None));
        Assert.False(MapHotkeyService.IsMatch(config, Key.X, ModifierKeys.Control));
        Assert.False(MapHotkeyService.IsMatch(config, Key.N, ModifierKeys.Alt));
    }

    [Fact]
    public void Validate_rejects_enabled_keyboard_hotkey_without_modifier()
    {
        var result = MapHotkeyService.Validate(new MapHotkeyConfig { Enabled = true, Alt = false, Key = "X" });

        Assert.False(result.Success);
        Assert.Contains("至少需要一个修饰键", result.ErrorMessage);
    }

    [Theory]
    [InlineData(false, true, false, "F4")]
    [InlineData(false, true, false, "Space")]
    [InlineData(true, true, false, "Delete")]
    public void Validate_rejects_high_risk_windows_keyboard_hotkeys(bool ctrl, bool alt, bool shift, string key)
    {
        var result = MapHotkeyService.Validate(new MapHotkeyConfig
        {
            Enabled = true,
            Ctrl = ctrl,
            Alt = alt,
            Shift = shift,
            Key = key
        });

        Assert.False(result.Success);
    }

    [Fact]
    public void HasSameGestureAsMainHotkey_detects_keyboard_conflict()
    {
        var main = new HotkeyConfig { Enabled = true, InputType = "Keyboard", Alt = true, Key = "X" };
        var map = new MapHotkeyConfig { Enabled = true, Alt = true, Key = "X" };

        Assert.True(MapHotkeyService.HasSameGestureAsMainHotkey(map, main));
        Assert.False(MapHotkeyService.HasSameGestureAsMainHotkey(
            new MapHotkeyConfig { Enabled = true, Ctrl = true, Key = "X" },
            main));
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Tab)]
    [InlineData(Key.Back)]
    [InlineData(Key.Delete)]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightAlt)]
    public void Validate_rejects_input_and_navigation_keys(Key key)
    {
        var result = MapHotkeyService.Validate(new MapHotkeyConfig
        {
            Enabled = true,
            Key = MapHotkeyService.GetDisplayKey(key)
        });

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.Z)]
    [InlineData(Key.D0)]
    [InlineData(Key.NumPad9)]
    [InlineData(Key.F1)]
    [InlineData(Key.F12)]
    public void Validate_accepts_supported_keys(Key key)
    {
        var result = MapHotkeyService.Validate(new MapHotkeyConfig
        {
            Enabled = true,
            Key = MapHotkeyService.GetDisplayKey(key)
        });

        Assert.True(result.Success, result.ErrorMessage);
    }

    [Fact]
    public void Validate_accepts_space_when_not_combined_with_alt()
    {
        var result = MapHotkeyService.Validate(new MapHotkeyConfig
        {
            Enabled = true,
            Ctrl = true,
            Alt = false,
            Key = "Space"
        });

        Assert.True(result.Success, result.ErrorMessage);
    }

    [Fact]
    public void Validate_rejects_empty_enabled_key()
    {
        var result = MapHotkeyService.Validate(new MapHotkeyConfig { Enabled = true, Key = " " });

        Assert.False(result.Success);
    }
}
