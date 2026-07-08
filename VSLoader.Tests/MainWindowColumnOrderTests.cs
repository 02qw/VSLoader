namespace VSLoader.Tests;

public sealed class MainWindowColumnOrderTests
{
    [Fact]
    public void Shortcut_grid_shows_source_module_before_description()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml"));

        var nameIndex = xaml.IndexOf("SortMemberPath=\"Name\"", StringComparison.Ordinal);
        var sourceModuleIndex = xaml.IndexOf("SortMemberPath=\"SourceModuleName\"", StringComparison.Ordinal);
        var descriptionIndex = xaml.IndexOf("SortMemberPath=\"Description\"", StringComparison.Ordinal);
        var updatedAtIndex = xaml.IndexOf("SortMemberPath=\"UpdatedAt\"", StringComparison.Ordinal);

        Assert.True(nameIndex >= 0);
        Assert.True(sourceModuleIndex > nameIndex);
        Assert.True(descriptionIndex > sourceModuleIndex);
        Assert.True(updatedAtIndex > descriptionIndex);
    }
}
