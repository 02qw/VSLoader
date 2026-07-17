namespace VSLoader.Tests;

public sealed class FactoryMapWindowMaximizeTests
{
    [Fact]
    public void Factory_map_window_uses_shared_title_bar_workspace_maximize_logic()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.DoesNotContain("StateChanged += FactoryMapWindow_StateChanged", code);
        Assert.DoesNotContain("FactoryMapWindow_StateChanged", code);
        Assert.DoesNotContain("ApplyMaximizedWorkingArea", code);
        Assert.DoesNotContain("ClearMaximizedWorkingAreaConstraint", code);
        Assert.DoesNotContain("isApplyingMaximizedWorkingArea", code);
    }

    [Fact]
    public void Factory_map_status_bar_stays_docked_at_bottom_without_fixed_taskbar_compensation()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml"));

        Assert.Contains("DockPanel.Dock=\"Bottom\"", xaml);
        Assert.Contains("x:Name=\"StatusText\"", xaml);
    }
}
