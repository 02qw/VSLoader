namespace VSLoader.Tests;

public sealed class FactoryMapWindowMaximizeTests
{
    [Fact]
    public void Factory_map_window_constrains_maximized_state_to_working_area()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("StateChanged += FactoryMapWindow_StateChanged", code);
        Assert.Contains("ApplyMaximizedWorkingArea", code);
        Assert.Contains("ClearMaximizedWorkingAreaConstraint", code);
        Assert.Contains("Screen.FromHandle", code);
        Assert.Contains("WorkingArea", code);
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
