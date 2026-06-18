using VSLoader.Views;

namespace VSLoader.Tests;

public sealed class FactoryMapDownloadAdminUiLinksTests
{
    [Fact]
    public void ShouldInvokeDownloadAdminUiLinks_returns_true_when_command_can_execute()
    {
        Assert.True(FactoryMapWindow.ShouldInvokeDownloadAdminUiLinks(canExecute: true));
    }

    [Fact]
    public void ShouldInvokeDownloadAdminUiLinks_returns_false_when_command_cannot_execute()
    {
        Assert.False(FactoryMapWindow.ShouldInvokeDownloadAdminUiLinks(canExecute: false));
    }
}
