namespace VSLoader.Tests;

public sealed class ModernThemeProductSystemTests
{
    [Fact]
    public void Modern_theme_contains_application_level_product_styles()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Styles",
            "ModernTheme.xaml"));

        Assert.Contains("x:Key=\"ModernQuietButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernDialogPrimaryButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernDialogSecondaryButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernContextMenuStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernMenuItemStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernInfoBannerStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernWarningBannerStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernErrorBannerStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernSuccessBannerStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernToolStripStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernDialogSurfaceStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernProgressBarStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernLogListBoxStyle\"", xaml);
    }

    [Fact]
    public void Modern_theme_does_not_override_native_scrollbars()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Styles",
            "ModernTheme.xaml"));

        Assert.DoesNotContain("TargetType=\"ScrollBar\"", xaml);
        Assert.DoesNotContain("x:Key=\"ModernScrollBarStyle\"", xaml);
    }
}
