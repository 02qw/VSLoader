using VSLoader.Views;

namespace VSLoader.Tests;

public sealed class MessageDialogWindowTests
{
    [Theory]
    [InlineData(MessageDialogKind.Confirm, false)]
    [InlineData(MessageDialogKind.Info, false)]
    [InlineData(MessageDialogKind.Error, false)]
    public void Close_button_never_confirms_dialog(MessageDialogKind kind, bool expected)
    {
        Assert.Equal(expected, MessageDialogWindow.ShouldConfirmOnClose(kind));
    }
}
