namespace VSLoader.Tests;

public sealed class MainWindowBannerTests
{
    [Fact]
    public void Update_banners_use_acknowledge_action_buttons()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml"));

        Assert.Contains("Content=\"我知道了\"", xaml);
        Assert.Contains("Style=\"{StaticResource ModernWarningBannerActionButtonStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource ModernErrorBannerActionButtonStyle}\"", xaml);
        Assert.Contains("Command=\"{Binding CloseUpdateNoticeCommand}\"", xaml);
        Assert.Contains("Command=\"{Binding CloseUpdateFailureCommand}\"", xaml);
        Assert.DoesNotContain("Content=\"×\"", xaml);
    }

    [Fact]
    public void Update_software_button_uses_software_update_notice_visual_state()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml"));
        var theme = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Styles",
            "ModernTheme.xaml"));

        Assert.Contains("Content=\"更新软件\"", xaml);
        Assert.Contains("Style=\"{StaticResource ModernUpdateSoftwareButtonStyle}\"", xaml);
        Assert.Contains("HasSoftwareUpdateNotice", theme);
        Assert.Contains("#16A34A", theme);
    }
}
