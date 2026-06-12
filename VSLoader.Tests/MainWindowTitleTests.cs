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
}
