namespace VSLoader.Tests;

public sealed class ModernWindowChromeResourceTests
{
    [Fact]
    public void ModernWindowChrome_defines_title_bar_styles_and_is_merged_in_app_resources()
    {
        var chromeXaml = File.ReadAllText(GetProjectFilePath("VSLoader", "Styles", "ModernWindowChrome.xaml"));
        var appXaml = File.ReadAllText(GetProjectFilePath("VSLoader", "App.xaml"));

        Assert.Contains("ModernTitleBarButtonStyle", chromeXaml);
        Assert.Contains("ModernTitleBarCloseButtonStyle", chromeXaml);
        Assert.Contains("ModernTitleBarBackgroundBrush", chromeXaml);
        Assert.Contains("ModernWindowOuterBorderBrush", chromeXaml);
        Assert.Contains("ModernTitleBarIconBrush", chromeXaml);
        Assert.Contains("ModernTitleBarIconHoverBrush", chromeXaml);
        Assert.Contains("ModernTitleBarCloseIconHoverBrush", chromeXaml);
        Assert.Contains("FocusVisualStyle", chromeXaml);
        Assert.Contains("{x:Null}", chromeXaml);
        Assert.Contains("Styles/ModernWindowChrome.xaml", appXaml);
    }

    private static string GetProjectFilePath(params string[] parts)
    {
        return TestProjectPaths.GetProjectFilePath(parts);
    }
}
