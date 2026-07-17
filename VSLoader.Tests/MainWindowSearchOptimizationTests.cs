namespace VSLoader.Tests;

public sealed class MainWindowSearchOptimizationTests
{
    [Fact]
    public void Main_search_binding_debounces_property_updates()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml"));

        Assert.Contains(
            "Text=\"{Binding SearchText, UpdateSourceTrigger=PropertyChanged, Delay=120}\"",
            xaml);
    }
}
