namespace VSLoader.Tests;

public sealed class SettingsWindowTabbedLayoutTests
{
    [Fact]
    public void Settings_window_uses_reorderable_tabs_and_independent_page_content()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "SettingsWindow.xaml"));

        Assert.Contains("x:Name=\"SettingsPageTabs\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding SettingsPages}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedSettingsPage", xaml, StringComparison.Ordinal);
        Assert.Contains("Orientation=\"Horizontal\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsGeneralPageSelected", xaml, StringComparison.Ordinal);
        Assert.Contains("IsAdminUiPageSelected", xaml, StringComparison.Ordinal);
        Assert.Contains("IsWebUiPageSelected", xaml, StringComparison.Ordinal);
        Assert.Contains("IsUpdatesPageSelected", xaml, StringComparison.Ordinal);
        Assert.Contains("IsHotkeysPageSelected", xaml, StringComparison.Ordinal);
        Assert.Contains("IsContextMenuCapabilitiesPageSelected", xaml, StringComparison.Ordinal);
        Assert.Contains("IsPageOrderPageSelected", xaml, StringComparison.Ordinal);
        Assert.Contains("MoveSettingsPageUpCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("MoveSettingsPageDownCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("RestoreDefaultSettingsPageOrderCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("页面顺序固定显示在最后", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_settings_page_keeps_touchpad_scrolling_and_fixed_footer_commands()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "SettingsWindow.xaml"));

        Assert.True(
            CountOccurrences(xaml, "behaviors:SmoothTouchpadScrollBehavior.IsEnabled=\"True\"") >= 7,
            "每个设置内容页都应拥有独立的触控板滚动区域。");
        Assert.Contains("Grid.Row=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SaveCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CancelCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_view_model_loads_and_saves_program_level_page_order()
    {
        var source = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "ViewModels",
            "MainViewModel.cs"));

        Assert.Contains("_appSettings.SettingsPageOrder", source, StringComparison.Ordinal);
        Assert.Contains("viewModel.SettingsPageOrder.ToList()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Wheel_forwarding_uses_the_current_page_scroll_viewer()
    {
        var source = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "SettingsWindow.xaml.cs"));

        Assert.Contains("FindAncestorScrollViewer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SettingsScrollViewer.VerticalOffset", source, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }
}
