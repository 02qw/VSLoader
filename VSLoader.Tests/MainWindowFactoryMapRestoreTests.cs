namespace VSLoader.Tests;

public sealed class MainWindowFactoryMapRestoreTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ShouldRestoreFactoryMapBounds_depends_on_session_bounds_not_main_window_state(
        bool useSessionBounds,
        bool hasFactoryMapBounds,
        bool expected)
    {
        Assert.Equal(expected, MainWindow.ShouldRestoreFactoryMapBounds(useSessionBounds, hasFactoryMapBounds));
    }

    [Fact]
    public void PositionFactoryMapWindow_does_not_skip_session_bounds_when_main_window_is_minimized()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.DoesNotContain("_factoryMapWindow is null || WindowState == WindowState.Minimized", code);
        Assert.Contains("ShouldRestoreFactoryMapBounds", code);
        Assert.Contains("CalculateDefaultFactoryMapBoundsWithoutMainWindow", code);
    }

    [Fact]
    public void Factory_map_user_close_saves_layout_immediately_and_closed_handler_only_cleans_up()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        var closeMethodStart = code.IndexOf("private void CloseFactoryMapByUserAction()", StringComparison.Ordinal);
        var closeMethodEnd = code.IndexOf("private void CloseFactoryMapForExit()", StringComparison.Ordinal);
        Assert.True(closeMethodStart >= 0);
        Assert.True(closeMethodEnd > closeMethodStart);
        var closeMethod = code[closeMethodStart..closeMethodEnd];
        Assert.Contains("SaveFactoryMapStateToSession", closeMethod);
        Assert.Contains("SaveWindowLayoutImmediately", closeMethod);

        var closedHandlerStart = code.IndexOf("_factoryMapWindow.Closed += (_, _) =>", StringComparison.Ordinal);
        var closedHandlerEnd = code.IndexOf("PositionFactoryMapWindow(useSessionBounds: true)", StringComparison.Ordinal);
        Assert.True(closedHandlerStart >= 0);
        Assert.True(closedHandlerEnd > closedHandlerStart);
        var closedHandler = code[closedHandlerStart..closedHandlerEnd];
        Assert.DoesNotContain("SaveFactoryMapStateToSession", closedHandler);
    }
}
