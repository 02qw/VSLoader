namespace VSLoader.Tests;

public sealed class FactoryMapWindowTitleBarTests
{
    [Fact]
    public void Factory_map_title_bar_close_is_routed_to_owner_close_request()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml"));
        var codeBehind = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));
        var mainWindowCode = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"MapTitleBar\"", xaml);
        Assert.Contains("public event EventHandler? CloseRequested", codeBehind);
        Assert.Contains("MapTitleBar.CloseRequested", codeBehind);
        Assert.Contains("CloseRequested?.Invoke", codeBehind);
        Assert.Contains("_factoryMapWindow.CloseRequested", mainWindowCode);
        Assert.Contains("CloseFactoryMapByUserAction", mainWindowCode);
    }
}
