using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class GlobalHotkeyServiceTests
{
    [Theory]
    [InlineData(false, true, false, "V")]
    [InlineData(true, false, false, "V")]
    [InlineData(false, false, true, "V")]
    public void Validate_allows_keyboard_hotkey_with_one_modifier(bool ctrl, bool alt, bool shift, string key)
    {
        var result = GlobalHotkeyService.Validate(CreateKeyboardHotkey(ctrl, alt, shift, key));

        Assert.True(result.Success, result.ErrorMessage);
    }

    [Theory]
    [InlineData(true, true, false, "V")]
    [InlineData(true, false, true, "Space")]
    [InlineData(true, true, true, "F8")]
    public void Validate_continues_to_allow_keyboard_hotkey_with_multiple_modifiers(bool ctrl, bool alt, bool shift, string key)
    {
        var result = GlobalHotkeyService.Validate(CreateKeyboardHotkey(ctrl, alt, shift, key));

        Assert.True(result.Success, result.ErrorMessage);
    }

    [Fact]
    public void Validate_rejects_keyboard_hotkey_without_modifier()
    {
        var result = GlobalHotkeyService.Validate(CreateKeyboardHotkey(false, false, false, "V"));

        Assert.False(result.Success);
        Assert.Contains("至少需要一个修饰键", result.ErrorMessage);
    }

    [Theory]
    [InlineData(false, true, false, "F4")]
    [InlineData(false, true, false, "Space")]
    [InlineData(true, true, false, "Delete")]
    public void Validate_rejects_high_risk_windows_keyboard_hotkeys(bool ctrl, bool alt, bool shift, string key)
    {
        var result = GlobalHotkeyService.Validate(CreateKeyboardHotkey(ctrl, alt, shift, key));

        Assert.False(result.Success);
    }

    [Fact]
    public void Validate_keeps_mouse_hotkey_modifier_requirement()
    {
        var noModifierResult = GlobalHotkeyService.Validate(CreateMouseHotkey(false, false, false, "Mouse4"));
        var ctrlMouse4Result = GlobalHotkeyService.Validate(CreateMouseHotkey(true, false, false, "Mouse4"));
        var altMouse5Result = GlobalHotkeyService.Validate(CreateMouseHotkey(false, true, false, "Mouse5"));

        Assert.False(noModifierResult.Success);
        Assert.True(ctrlMouse4Result.Success, ctrlMouse4Result.ErrorMessage);
        Assert.True(altMouse5Result.Success, altMouse5Result.ErrorMessage);
    }

    private static HotkeyConfig CreateKeyboardHotkey(bool ctrl, bool alt, bool shift, string key)
    {
        return new HotkeyConfig
        {
            Enabled = true,
            Ctrl = ctrl,
            Alt = alt,
            Shift = shift,
            InputType = "Keyboard",
            Key = key
        };
    }

    private static HotkeyConfig CreateMouseHotkey(bool ctrl, bool alt, bool shift, string key)
    {
        return new HotkeyConfig
        {
            Enabled = true,
            Ctrl = ctrl,
            Alt = alt,
            Shift = shift,
            InputType = "Mouse",
            Key = key
        };
    }
}
