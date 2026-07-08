using System.Windows;

namespace VSLoader.Tests;

public sealed class MainWindowFactoryMapCloseTests
{
    [Theory]
    [InlineData(WindowState.Minimized, false)]
    [InlineData(WindowState.Normal, false)]
    [InlineData(WindowState.Maximized, false)]
    public void ShouldCloseFactoryMapOnStateChanged_never_closes_from_window_state(
        WindowState state,
        bool expected)
    {
        Assert.Equal(expected, MainWindow.ShouldCloseFactoryMapOnStateChanged(state));
    }

    [Fact]
    public void MainWindow_no_longer_contains_main_window_driven_map_restore_or_hide_helpers()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.DoesNotContain("ShouldRestoreFactoryMapWithMainWindow", code);
        Assert.DoesNotContain("ShouldActivateFactoryMapWhenRestoringFromBackground", code);
        Assert.DoesNotContain("HideFactoryMapWindow", code);
        Assert.DoesNotContain("RestoreFactoryMapForBackgroundActivation", code);
    }

    [Theory]
    [InlineData(true, true, WindowState.Minimized, true)]
    [InlineData(true, true, WindowState.Normal, false)]
    [InlineData(true, true, WindowState.Maximized, false)]
    [InlineData(false, true, WindowState.Minimized, false)]
    [InlineData(true, false, WindowState.Minimized, false)]
    public void ShouldRestoreMinimizedFactoryMapOnToggle_only_restores_existing_minimized_open_map(
        bool isFactoryMapOpen,
        bool hasFactoryMapWindow,
        WindowState factoryMapWindowState,
        bool expected)
    {
        Assert.Equal(expected, MainWindow.ShouldRestoreMinimizedFactoryMapOnToggle(
            isFactoryMapOpen,
            hasFactoryMapWindow,
            factoryMapWindowState));
    }

}
