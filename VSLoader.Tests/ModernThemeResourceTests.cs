namespace VSLoader.Tests;

public sealed class ModernThemeResourceTests
{
    [Fact]
    public void ModernTheme_contains_core_style_keys()
    {
        var xaml = ReadModernThemeXaml();

        Assert.Contains("ModernButtonStyle", xaml);
        Assert.Contains("ModernPrimaryButtonStyle", xaml);
        Assert.Contains("ModernDangerButtonStyle", xaml);
        Assert.Contains("ModernTextBoxStyle", xaml);
        Assert.Contains("ModernSurfaceBorderStyle", xaml);
        Assert.Contains("ModernDataGridStyle", xaml);
        Assert.Contains("ModernBusyPanelStyle", xaml);
    }

    [Fact]
    public void Modern_input_templates_do_not_double_apply_text_padding()
    {
        var xaml = ReadModernThemeXaml();
        var inputStyleBlocks = new[]
        {
            ExtractStyleBlock(xaml, "ModernTextBoxStyle"),
            ExtractStyleBlock(xaml, "ModernMultilineTextBoxStyle"),
            ExtractStyleBlock(xaml, "ModernPasswordBoxStyle")
        };

        foreach (var styleBlock in inputStyleBlocks)
        {
            Assert.DoesNotContain("Margin=\"{TemplateBinding Padding}\"", styleBlock);
            Assert.DoesNotContain("Margin=\"10,0\"", styleBlock);
            Assert.DoesNotContain("Margin=\"10,8\"", styleBlock);
            Assert.DoesNotContain("Padding=\"{TemplateBinding Padding}\"", styleBlock);
            Assert.Contains("PART_ContentHost", styleBlock);
        }
    }

    [Fact]
    public void Modern_button_styles_replace_default_dotted_focus_visual()
    {
        var xaml = ReadModernThemeXaml();

        Assert.Contains("x:Key=\"ModernButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernPrimaryButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernDangerButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernIconButtonStyle\"", xaml);
        Assert.True(CountOccurrences(xaml, "Property=\"FocusVisualStyle\" Value=\"{x:Null}\"") >= 4);
        Assert.True(CountOccurrences(xaml, "Property=\"IsKeyboardFocused\" Value=\"True\"") >= 3);
    }

    [Fact]
    public void Modern_toolbar_toggle_button_style_separates_active_state_from_keyboard_focus()
    {
        var xaml = ReadModernThemeXaml();
        var normalButtonBlock = ExtractStyleBlock(xaml, "ModernButtonStyle");
        var toggleButtonBlock = ExtractStyleBlock(xaml, "ModernToolbarToggleButtonStyle");

        Assert.Contains("ModernActiveButtonBackgroundBrush", xaml);
        Assert.Contains("ModernActiveButtonBorderBrush", xaml);
        Assert.Contains("ModernFocusBorderBrush", xaml);
        Assert.Contains("Property=\"Tag\" Value=\"Active\"", toggleButtonBlock);
        Assert.Contains("ModernActiveButtonBackgroundBrush", toggleButtonBlock);
        Assert.Contains("ModernActiveButtonBorderBrush", toggleButtonBlock);
        Assert.DoesNotContain("BorderBrush\" Value=\"{StaticResource ModernPrimaryBrush}\"", normalButtonBlock);
    }

    [Fact]
    public void ModernTheme_contains_banner_action_button_styles()
    {
        var xaml = ReadModernThemeXaml();

        Assert.Contains("x:Key=\"ModernBannerActionButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernWarningBannerActionButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"ModernErrorBannerActionButtonStyle\"", xaml);
        Assert.Contains("Property=\"FocusVisualStyle\" Value=\"{x:Null}\"", xaml);
        Assert.Contains("Property=\"IsMouseOver\" Value=\"True\"", xaml);
        Assert.Contains("Property=\"IsPressed\" Value=\"True\"", xaml);
        Assert.Contains("Property=\"IsKeyboardFocused\" Value=\"True\"", xaml);
    }

    [Fact]
    public void ModernTheme_does_not_override_global_scrollbar_visuals()
    {
        var xaml = ReadModernThemeXaml();

        Assert.DoesNotContain("x:Key=\"ModernScrollBarStyle\"", xaml);
        Assert.DoesNotContain("TargetType=\"ScrollBar\" BasedOn=", xaml);
    }

    [Fact]
    public void Modern_multiline_textbox_uses_auto_height_instead_of_null_height()
    {
        var xaml = ReadModernThemeXaml();

        Assert.Contains("x:Key=\"ModernMultilineTextBoxStyle\"", xaml);
        Assert.Contains("Property=\"Height\" Value=\"Auto\"", xaml);
        Assert.DoesNotContain("Property=\"Height\" Value=\"{x:Null}\"", xaml);
    }

    private static string ReadModernThemeXaml()
    {
        return File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Styles",
            "ModernTheme.xaml"));
    }

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = 0;

        while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    private static string ExtractStyleBlock(string xaml, string styleKey)
    {
        var startToken = $"x:Key=\"{styleKey}\"";
        var startIndex = xaml.IndexOf(startToken, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Style {styleKey} should exist.");

        var nextStyleIndex = xaml.IndexOf("<Style ", startIndex + startToken.Length, StringComparison.Ordinal);
        return nextStyleIndex < 0
            ? xaml[startIndex..]
            : xaml[startIndex..nextStyleIndex];
    }
}
