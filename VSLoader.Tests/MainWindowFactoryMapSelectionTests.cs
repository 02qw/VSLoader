namespace VSLoader.Tests;

public sealed class MainWindowFactoryMapSelectionTests
{
    [Fact]
    public void SelectShortcutFromMap_does_not_focus_main_grid()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.Contains("SelectShortcutInGrid(viewModel, shortcut, focusGrid: false)", code);
        Assert.Contains("private void SelectShortcutInGrid(MainViewModel viewModel, VSLoader.Models.ShortcutItem shortcut, bool focusGrid)", code);
        Assert.Contains("if (focusGrid)", code);
        Assert.Contains("ShortcutsGrid.Focus();", code);
        Assert.DoesNotContain("SelectShortcutInGrid(viewModel, shortcut);", code);
    }

    [Fact]
    public void Factory_map_edit_uses_map_owned_edit_dialog_without_main_edit_command()
    {
        var mainWindowCode = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));
        var editWindowCode = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "ShortcutEditWindow.xaml.cs"));

        Assert.Contains("EditShortcutFromMap(shortcut)", mainWindowCode);
        Assert.DoesNotContain("viewModel.EditShortcutCommand.Execute(null)", mainWindowCode);
        Assert.Contains("ShortcutEditWindow(ShortcutEditViewModel viewModel, Window? owner = null)", editWindowCode);
        Assert.Contains("Owner = owner ?? System.Windows.Application.Current.MainWindow", editWindowCode);
    }
}
