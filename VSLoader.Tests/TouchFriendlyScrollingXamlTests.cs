namespace VSLoader.Tests;

public sealed class TouchFriendlyScrollingXamlTests
{
    [Fact]
    public void Main_grid_keeps_virtualization_while_using_touchpad_behavior()
    {
        var xaml = ReadProjectFile("VSLoader", "MainWindow.xaml");

        Assert.Contains("xmlns:behaviors=\"clr-namespace:VSLoader.Behaviors\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.IsEnabled=\"True\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.EnableHorizontal=\"True\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.EnableVertical=\"True\"", xaml);
        Assert.DoesNotContain("behaviors:SmoothTouchpadScrollBehavior.VerticalSensitivity=\"0.04\"", xaml);
        Assert.Contains("EnableRowVirtualization=\"True\"", xaml);
        Assert.Contains("EnableColumnVirtualization=\"True\"", xaml);
        Assert.Contains("VirtualizingPanel.ScrollUnit=\"Pixel\"", xaml);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", xaml);
    }

    [Fact]
    public void Settings_scroll_viewer_uses_vertical_touchpad_behavior()
    {
        var xaml = ReadProjectFile("VSLoader", "Views", "SettingsWindow.xaml");

        Assert.Contains("xmlns:behaviors=\"clr-namespace:VSLoader.Behaviors\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.IsEnabled=\"True\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.EnableHorizontal=\"False\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.EnableVertical=\"True\"", xaml);
        Assert.Contains("SettingsInput_PreviewMouseWheel", xaml);
    }

    [Fact]
    public void Batch_import_grid_supports_horizontal_and_vertical_touchpad_behavior()
    {
        var xaml = ReadProjectFile("VSLoader", "Views", "BatchImportWindow.xaml");

        Assert.Contains("xmlns:behaviors=\"clr-namespace:VSLoader.Behaviors\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.IsEnabled=\"True\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.EnableHorizontal=\"True\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.EnableVertical=\"True\"", xaml);
    }

    [Fact]
    public void Workspace_list_uses_vertical_touchpad_behavior()
    {
        var xaml = ReadProjectFile("VSLoader", "Views", "WorkspaceSelectorWindow.xaml");

        Assert.Contains("xmlns:behaviors=\"clr-namespace:VSLoader.Behaviors\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.IsEnabled=\"True\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.EnableHorizontal=\"False\"", xaml);
        Assert.Contains("behaviors:SmoothTouchpadScrollBehavior.EnableVertical=\"True\"", xaml);
    }

    private static string ReadProjectFile(params string[] parts)
    {
        return File.ReadAllText(TestProjectPaths.GetProjectFilePath(parts));
    }
}
