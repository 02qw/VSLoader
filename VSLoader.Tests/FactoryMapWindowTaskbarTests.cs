namespace VSLoader.Tests;

public sealed class FactoryMapWindowTaskbarTests
{
    [Fact]
    public void Factory_map_window_is_shown_in_taskbar()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml"));

        Assert.Contains("ShowInTaskbar=\"True\"", xaml);
        Assert.DoesNotContain("ShowInTaskbar=\"False\"", xaml);
    }
}
