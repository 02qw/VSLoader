namespace VSLoader.Tests;

public sealed class MainWindowTitleTests
{
    [Fact]
    public void FormatWindowTitle_shows_three_part_version()
    {
        Assert.Equal("VSLoader v1.7.4", MainWindow.FormatWindowTitle(new Version(1, 7, 4)));
    }

    [Fact]
    public void FormatWindowTitle_shows_two_part_version_when_build_is_missing()
    {
        Assert.Equal("VSLoader v1.7", MainWindow.FormatWindowTitle(new Version(1, 7)));
    }

    [Fact]
    public void FormatWindowTitle_falls_back_when_version_is_null()
    {
        Assert.Equal("VSLoader", MainWindow.FormatWindowTitle(null));
    }

    [Fact]
    public void FormatWindowTitleWithWorkspace_appends_workspace_name()
    {
        Assert.Equal("VSLoader v1.7.4 - 默认工作区", MainWindow.FormatWindowTitleWithWorkspace("VSLoader v1.7.4", "默认工作区"));
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    public void ShouldRestoreFromHotkey_depends_on_visibility_minimized_state_and_app_activation(
        bool isVisible,
        bool isMinimized,
        bool isVsLoaderActive,
        bool expected)
    {
        Assert.Equal(expected, MainWindow.ShouldRestoreFromHotkey(isVisible, isMinimized, isVsLoaderActive));
    }
}
