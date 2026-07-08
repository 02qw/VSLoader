namespace VSLoader.Tests;

public sealed class ModernThemeProductVisualTests
{
    [Fact]
    public void Modern_theme_contains_product_button_styles()
    {
        var xaml = ReadModernThemeXaml();

        Assert.Contains("x:Key=\"ModernQuietButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernDangerButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernUpdateSoftwareButtonStyle\"", xaml);
        Assert.Contains("HasSoftwareUpdateNotice", xaml);
    }

    [Fact]
    public void Modern_theme_contains_product_data_grid_styles()
    {
        var xaml = ReadModernThemeXaml();

        Assert.Contains("x:Key=\"ModernDataGridStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernDataGridColumnHeaderStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernDataGridRowStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernDataGridCellStyle\"", xaml);
    }

    private static string ReadModernThemeXaml()
    {
        return File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Styles",
            "ModernTheme.xaml"));
    }
}
