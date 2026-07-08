namespace VSLoader.Tests;

public sealed class MainViewModelAdminUiAutoPasteSourceTests
{
    [Fact]
    public void OpenAdminUiAsync_invokes_auto_paste_only_after_clipboard_success()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "ViewModels",
            "MainViewModel.cs"));
        var methodBlock = ExtractMethodBlock(code, "private async Task OpenAdminUiAsync()");

        Assert.Contains("var clipboardResult = await _clipboardService.SetTextWithRetryAsync(password);", methodBlock);
        Assert.Contains("_adminUiAutoPasteLogService.LogClipboardCheck", methodBlock);
        Assert.Contains("if (!adminUiConfig.AutoPastePasswordEnabled)", methodBlock);
        Assert.Contains("var pasteResult = await _adminUiAutoPasteService.TryPasteAsync(adminUiConfig);", methodBlock);
        Assert.Contains("密码已自动粘贴并回车", methodBlock);
        Assert.Contains("请手动粘贴", methodBlock);

        var clipboardIndex = methodBlock.IndexOf("clipboardResult.Success", StringComparison.Ordinal);
        var clipboardCheckIndex = methodBlock.IndexOf("_adminUiAutoPasteLogService.LogClipboardCheck", StringComparison.Ordinal);
        var pasteIndex = methodBlock.IndexOf("_adminUiAutoPasteService.TryPasteAsync", StringComparison.Ordinal);
        Assert.True(clipboardIndex >= 0);
        Assert.True(clipboardCheckIndex > clipboardIndex);
        Assert.True(pasteIndex > clipboardIndex);
    }

    [Fact]
    public void Settings_window_exposes_safe_adminui_auto_paste_options()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "SettingsWindow.xaml"));

        Assert.Contains("Content=\"自动粘贴密码并回车\"", xaml);
        Assert.Contains("AdminUi.AutoPastePasswordEnabled", xaml);
        Assert.Contains("Text=\"登录窗口标题关键字\"", xaml);
        Assert.Contains("AdminUi.AutoPasteWindowTitleKeyword", xaml);
        Assert.Contains("Text=\"等待超时秒数\"", xaml);
        Assert.Contains("AdminUi.AutoPasteTimeoutSeconds", xaml);
    }

    private static string ExtractMethodBlock(string code, string methodSignature)
    {
        var methodStart = code.IndexOf(methodSignature, StringComparison.Ordinal);
        Assert.True(methodStart >= 0);

        var nextMethodStart = code.IndexOf("\n    [RelayCommand", methodStart + methodSignature.Length, StringComparison.Ordinal);
        Assert.True(nextMethodStart >= 0);

        return code[methodStart..nextMethodStart];
    }
}
