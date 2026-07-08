using VSLoader.Views;

namespace VSLoader.Tests;

public sealed class MapHotkeyTriggerTests
{
    [Theory]
    [InlineData(false, false, false, false, FactoryMapHotkeyAction.Open)]
    [InlineData(true, true, false, false, FactoryMapHotkeyAction.Restore)]
    [InlineData(true, false, false, false, FactoryMapHotkeyAction.Activate)]
    [InlineData(true, false, true, false, FactoryMapHotkeyAction.Minimize)]
    [InlineData(true, false, true, true, FactoryMapHotkeyAction.Ignore)]
    public void MainWindow_selects_map_global_hotkey_action_from_independent_window_state(
        bool hasFactoryMapWindow,
        bool isMinimized,
        bool isActive,
        bool isBlocked,
        FactoryMapHotkeyAction expected)
    {
        Assert.Equal(expected, MainWindow.GetFactoryMapHotkeyAction(
            hasFactoryMapWindow,
            isMinimized,
            isActive,
            isBlocked));
    }

    [Fact]
    public void MainWindow_uses_global_hotkey_instead_of_preview_keydown_for_map_hotkey()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.Contains("ToggleFactoryMapFromGlobalHotkey", code);
        Assert.DoesNotContain("MainWindow_PreviewKeyDown", code);
        Assert.DoesNotContain("TryToggleFactoryMapFromHotkey", code);
    }
}
