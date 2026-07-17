namespace VSLoader.Tests;

public sealed class MainWindowProductVisualTests
{
    [Fact]
    public void Main_window_splits_toolbar_into_product_action_rows()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("x:Name=\"SearchBarRow\"", xaml);
        Assert.Contains("x:Name=\"PrimaryActionRow\"", xaml);
        Assert.Contains("x:Name=\"UtilityActionRow\"", xaml);
    }

    [Fact]
    public void Main_window_preserves_all_existing_action_entry_points()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("AddShortcutCommand", xaml);
        Assert.Contains("OpenSettingsCommand", xaml);
        Assert.Contains("UpdateSoftwareCommand", xaml);
        Assert.Contains("ManualCheckUpdatesCommand", xaml);
        Assert.Contains("ExportGlobalConfigCommand", xaml);
        Assert.Contains("ImportGlobalConfigCommand", xaml);
        Assert.Contains("OpenBatchImportCommand", xaml);
        Assert.Contains("DownloadAdminUiLinksCommand", xaml);
        Assert.Contains("OpenAdminUiCommand", xaml);
        Assert.Contains("OpenShortcutCommand", xaml);
        Assert.Contains("EditShortcutCommand", xaml);
        Assert.Contains("DeleteShortcutCommand", xaml);
        Assert.Contains("FactoryMapButton_Click", xaml);
        Assert.Contains("WorkspaceButton_Click", xaml);
    }

    [Fact]
    public void Main_window_uses_settings_icon_for_settings_button()
    {
        var xaml = ReadMainWindowXaml();
        var project = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "VSLoader.csproj"));

        Assert.Contains("Assets\\setting3.svg", project);
        Assert.Contains("<Viewbox Width=\"21\"", xaml);
        Assert.Contains("<Path Fill=\"#2C2C2C\"", xaml);
        Assert.Contains("Data=\"M1072.147851", xaml);
        Assert.DoesNotContain("Source=\"Assets/setting2.png\"", xaml);
        Assert.Contains("Command=\"{Binding OpenSettingsCommand}\"", xaml);
        Assert.Contains("ToolTip=\"设置\"", xaml);
        Assert.Contains("Width=\"42\"", xaml);
        Assert.Contains("Style=\"{StaticResource ModernSettingsIconButtonStyle}\"", xaml);
        Assert.DoesNotContain("Width=\"76\"\r\n                            Height=\"34\"\r\n                            Margin=\"12,0,0,0\"\r\n                            Command=\"{Binding OpenSettingsCommand}\"", xaml);
    }

    [Fact]
    public void Main_window_uses_clear_button_visual_hierarchy()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("Style=\"{StaticResource ModernPrimaryButtonStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource ModernUpdateSoftwareButtonStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource ModernDangerButtonStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource ModernQuietButtonStyle}\"", xaml);
    }

    [Fact]
    public void Main_toolbar_buttons_restore_content_focus_after_clicks()
    {
        var xaml = ReadMainWindowXaml();
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"PrimaryActionRow\"", xaml);
        Assert.Contains("x:Name=\"UtilityActionRow\"", xaml);
        Assert.Contains("Button.Click=\"ToolbarButton_Click\"", xaml);
        Assert.Contains("private void ToolbarButton_Click(object sender, RoutedEventArgs e)", code);
        Assert.Contains("RestoreMainContentFocusAfterToolbarClick();", code);
        Assert.Contains("private void RestoreMainContentFocusAfterToolbarClick()", code);
    }

    [Fact]
    public void Shortcut_grid_keeps_resizable_columns_and_uses_denser_table_metrics()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("CanUserResizeColumns=\"True\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding ShortcutsView}\"", xaml);
        Assert.True(xaml.Contains("RowHeight=\"40\"", StringComparison.Ordinal)
            || xaml.Contains("RowHeight=\"38\"", StringComparison.Ordinal));
        Assert.True(xaml.Contains("ColumnHeaderHeight=\"36\"", StringComparison.Ordinal)
            || xaml.Contains("ColumnHeaderHeight=\"38\"", StringComparison.Ordinal));
    }

    private static string ReadMainWindowXaml()
    {
        return File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml"));
    }
}
