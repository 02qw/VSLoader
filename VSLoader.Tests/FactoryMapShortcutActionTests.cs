using VSLoader.Models;

namespace VSLoader.Tests;

public sealed class FactoryMapShortcutActionTests
{
    [Fact]
    public void ActionsFollowShortcutContextMenuOrder()
    {
        var actions = Enum.GetValues<FactoryMapShortcutAction>();

        Assert.Equal(
            [
                FactoryMapShortcutAction.OpenVsCode,
                FactoryMapShortcutAction.OpenAdminUi,
                FactoryMapShortcutAction.DownloadAdminUiLink,
                FactoryMapShortcutAction.OpenWebUi,
                FactoryMapShortcutAction.Edit,
                FactoryMapShortcutAction.Delete
            ],
            actions);
    }
}
