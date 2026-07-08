using VSLoader;

namespace VSLoader.Tests;

public sealed class MainWindowExitStateTests
{
    [Fact]
    public void ShouldBeginShutdown_returns_true_for_first_exit_request()
    {
        Assert.True(MainWindow.ShouldBeginShutdown(isShutdownInProgress: false));
    }

    [Fact]
    public void ShouldBeginShutdown_returns_false_when_shutdown_is_already_running()
    {
        Assert.False(MainWindow.ShouldBeginShutdown(isShutdownInProgress: true));
    }
}
