namespace VSLoader.Tests;

public sealed class MainViewModelAdminUiDownloadDialogOrderTests
{
    [Fact]
    public void Download_all_adminui_links_defers_result_dialog_until_busy_overlay_is_cleared()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "ViewModels",
            "MainViewModel.cs"));
        var methodBlock = ExtractMethodBlock(code, "private async Task DownloadAdminUiLinksAsync()");

        Assert.Contains("string? pendingInfoMessage = null;", methodBlock);
        Assert.Contains("string? pendingErrorMessage = null;", methodBlock);
        Assert.Contains("pendingInfoMessage = message;", methodBlock);
        Assert.Contains("pendingErrorMessage = testResult.ErrorMessage", methodBlock);
        Assert.Contains("pendingErrorMessage = $\"自动获取连接失败：{ex.Message}\";", methodBlock);

        var finallyIndex = methodBlock.IndexOf("finally", StringComparison.Ordinal);
        var clearIndex = methodBlock.IndexOf("ClearBusyState();", finallyIndex, StringComparison.Ordinal);
        var showErrorIndex = methodBlock.IndexOf("_dialogService.ShowError(pendingErrorMessage);", StringComparison.Ordinal);
        var showInfoIndex = methodBlock.IndexOf("_dialogService.ShowInfo(pendingInfoMessage);", StringComparison.Ordinal);

        Assert.True(finallyIndex >= 0);
        Assert.True(clearIndex > finallyIndex);
        Assert.True(showErrorIndex > clearIndex);
        Assert.True(showInfoIndex > clearIndex);
    }

    [Fact]
    public void Download_selected_adminui_link_defers_result_dialog_until_busy_overlay_is_cleared()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "ViewModels",
            "MainViewModel.cs"));
        var methodBlock = ExtractMethodBlock(code, "private async Task DownloadSelectedAdminUiLinkAsync()");

        var awaitIndex = methodBlock.IndexOf(
            "var result = await DownloadAdminUiLinkForShortcutAsync(SelectedShortcut, BusyOverlayHost.Main);",
            StringComparison.Ordinal);
        var showErrorIndex = methodBlock.IndexOf("_dialogService.ShowError(result.Message);", StringComparison.Ordinal);
        var showInfoIndex = methodBlock.IndexOf("_dialogService.ShowInfo(result.Message);", StringComparison.Ordinal);
        var helperIndex = methodBlock.IndexOf(
            "private async Task<ContextMenuCapabilityExecutionResult> DownloadAdminUiLinkForShortcutAsync",
            StringComparison.Ordinal);
        var finallyIndex = methodBlock.IndexOf("finally", helperIndex, StringComparison.Ordinal);
        var clearIndex = methodBlock.IndexOf("ClearBusyState();", finallyIndex, StringComparison.Ordinal);

        Assert.True(awaitIndex >= 0);
        Assert.True(showErrorIndex > awaitIndex);
        Assert.True(showInfoIndex > awaitIndex);
        Assert.True(helperIndex > showInfoIndex);
        Assert.True(finallyIndex > helperIndex);
        Assert.True(clearIndex > finallyIndex);
    }

    private static string ExtractMethodBlock(string code, string methodSignature)
    {
        var methodStart = code.IndexOf(methodSignature, StringComparison.Ordinal);
        Assert.True(methodStart >= 0);

        var nextCommandIndex = code.IndexOf("\n    [RelayCommand", methodStart + methodSignature.Length, StringComparison.Ordinal);
        Assert.True(nextCommandIndex > methodStart);

        return code[methodStart..nextCommandIndex];
    }
}
