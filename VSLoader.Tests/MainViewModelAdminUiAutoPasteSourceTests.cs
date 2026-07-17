using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class MainViewModelAdminUiAutoPasteSourceTests
{
    [Fact]
    public void OpenAdminUiAsync_uses_direct_background_input_when_automation_is_enabled()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath("VSLoader", "ViewModels", "MainViewModel.cs"));
        var methodBlock = ExtractMethodBlock(code, "private async Task OpenAdminUiAsync()");

        Assert.Contains("if (adminUiConfig.AutoPastePasswordEnabled)", methodBlock);
        Assert.Contains("_adminUiAutoLoginCoordinator.Start(", methodBlock);
        Assert.Contains("adminUiConfig,", methodBlock);
        Assert.Contains("password,", methodBlock);
        Assert.Contains("var clipboardResult = await _clipboardService.SetTextWithRetryAsync(password);", methodBlock);
        Assert.DoesNotContain("AdminUiAutoInputMode", methodBlock);
        Assert.DoesNotContain("LogAdminUiClipboardCheck", methodBlock);

        var automationIndex = methodBlock.IndexOf("if (adminUiConfig.AutoPastePasswordEnabled)", StringComparison.Ordinal);
        var clipboardIndex = methodBlock.IndexOf("SetTextWithRetryAsync", StringComparison.Ordinal);
        Assert.True(automationIndex >= 0);
        Assert.True(clipboardIndex > automationIndex);
    }

    [Theory]
    [InlineData(AdminUiAutoLoginStatus.FocusLostBeforeInput)]
    [InlineData(AdminUiAutoLoginStatus.FocusLostBeforeEnter)]
    [InlineData(AdminUiAutoLoginStatus.TimedOut)]
    [InlineData(AdminUiAutoLoginStatus.PasswordEmpty)]
    [InlineData(AdminUiAutoLoginStatus.InputFailed)]
    public void Failed_auto_login_statuses_require_clipboard_fallback(AdminUiAutoLoginStatus status)
    {
        Assert.True(VSLoader.ViewModels.MainViewModel.ShouldUseAdminUiClipboardFallback(status));
    }

    [Fact]
    public void Submitted_input_does_not_require_clipboard_fallback()
    {
        Assert.False(VSLoader.ViewModels.MainViewModel.ShouldUseAdminUiClipboardFallback(
            AdminUiAutoLoginStatus.InputSubmitted));
    }

    [Fact]
    public void Auto_login_callbacks_dispatch_clipboard_fallback_to_ui_thread()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath("VSLoader", "ViewModels", "MainViewModel.cs"));

        Assert.Contains("BeginAdminUiClipboardFallback", code);
        Assert.Contains("dispatcher.InvokeAsync", code);
        Assert.Contains("SetTextWithRetryAsync(password", code);
        Assert.Contains("IsCurrentSession", code);
        Assert.Contains("密码已复制到剪贴板", code);
        Assert.Contains("密码写入剪贴板也失败", code);
    }

    [Fact]
    public void Application_exit_explicitly_stops_adminui_automation()
    {
        var viewModelCode = File.ReadAllText(TestProjectPaths.GetProjectFilePath("VSLoader", "ViewModels", "MainViewModel.cs"));
        var mainWindowCode = File.ReadAllText(TestProjectPaths.GetProjectFilePath("VSLoader", "MainWindow.xaml.cs"));

        Assert.Contains("ShutdownAdminUiAutomation", viewModelCode);
        Assert.Contains("viewModel.ShutdownAdminUiAutomation();", mainWindowCode);
    }

    [Fact]
    public void Settings_window_exposes_adminui_auto_login_options()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath("VSLoader", "Views", "SettingsWindow.xaml"));

        Assert.Contains("Content=\"自动粘贴密码并回车\"", xaml);
        Assert.Contains("AdminUi.AutoPastePasswordEnabled", xaml);
        Assert.Contains("AdminUi.AutoPasteWindowTitleKeyword", xaml);
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
