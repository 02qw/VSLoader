namespace VSLoader.Tests;

public sealed class ModernWindowChromeUsageTests
{
    [Theory]
    [InlineData("VSLoader", "MainWindow.xaml")]
    [InlineData("VSLoader", "Views", "WorkspaceSelectorWindow.xaml")]
    [InlineData("VSLoader", "Views", "SettingsWindow.xaml")]
    [InlineData("VSLoader", "Views", "BatchImportWindow.xaml")]
    [InlineData("VSLoader", "Views", "ShortcutEditWindow.xaml")]
    [InlineData("VSLoader", "Views", "WorkspaceNameDialog.xaml")]
    [InlineData("VSLoader", "Views", "FactoryMapWindow.xaml")]
    public void Core_windows_use_modern_window_chrome_and_title_bar(params string[] parts)
    {
        var xaml = File.ReadAllText(GetProjectFilePath(parts));

        Assert.Contains("WindowChrome.WindowChrome", xaml);
        Assert.Contains("ModernTitleBar", xaml);
        Assert.Contains("ModernWindowOuterBorderBrush", xaml);
        Assert.DoesNotContain("UseAeroCaptionButtons=\"True\"", xaml);
    }

    [Fact]
    public void Message_dialog_keeps_custom_borderless_shell()
    {
        var xaml = File.ReadAllText(GetProjectFilePath("VSLoader", "Views", "MessageDialogWindow.xaml"));

        Assert.Contains("WindowStyle=\"None\"", xaml);
        Assert.Contains("AllowsTransparency=\"True\"", xaml);
        Assert.Contains("ModernTitleBar", xaml);
        Assert.Contains("ModernWindowOuterBorderBrush", xaml);
    }

    [Fact]
    public void Modern_title_bar_uses_vector_icons_instead_of_font_glyphs()
    {
        var xaml = File.ReadAllText(GetProjectFilePath("VSLoader", "Views", "Controls", "ModernTitleBar.xaml"));

        Assert.DoesNotContain("Content=\"—\"", xaml);
        Assert.DoesNotContain("Content=\"□\"", xaml);
        Assert.DoesNotContain("Content=\"×\"", xaml);
        Assert.Contains("<Line", xaml);
        Assert.Contains("<Rectangle", xaml);
        Assert.Contains("MaximizeIcon", xaml);
        Assert.Contains("RestoreIcon", xaml);
    }

    private static string GetProjectFilePath(params string[] parts)
    {
        return TestProjectPaths.GetProjectFilePath(parts);
    }
}
