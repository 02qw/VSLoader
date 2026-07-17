namespace VSLoader.Tests;

public sealed class MainWindowSearchBoxTests
{
    [Fact]
    public void Main_search_box_uses_modern_textbox_spacing_without_extra_local_padding()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml"));
        var searchBoxBlock = ExtractSearchBoxBlock(xaml);

        Assert.Contains("Style=\"{StaticResource ModernTextBoxStyle}\"", searchBoxBlock);
        Assert.Contains(
            "Text=\"{Binding SearchText, UpdateSourceTrigger=PropertyChanged, Delay=120}\"",
            searchBoxBlock);
        Assert.DoesNotContain("Padding=", searchBoxBlock);
        Assert.DoesNotContain("VerticalContentAlignment=", searchBoxBlock);
    }

    [Fact]
    public void Main_window_clears_text_input_focus_when_clicking_non_input_area()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));
        var handlerBlock = ExtractMethodBlock(code, "private void MainWindow_PreviewMouseDown");

        Assert.Contains("PreviewMouseDown += MainWindow_PreviewMouseDown;", code);
        Assert.Contains("Keyboard.ClearFocus();", handlerBlock);
        Assert.Contains("Keyboard.Focus(MainFocusTarget);", handlerBlock);
        Assert.Contains("IsTextInputElement(e.OriginalSource as DependencyObject)", handlerBlock);
        Assert.DoesNotContain("e.Handled = true", handlerBlock);
    }

    [Fact]
    public void Main_window_declares_non_text_focus_target_for_search_box_blur()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml"));

        Assert.Contains("x:Name=\"MainFocusTarget\"", xaml);
        Assert.Contains("Focusable=\"True\"", xaml);
    }

    private static string ExtractSearchBoxBlock(string xaml)
    {
        var searchTextIndex = xaml.IndexOf("Text=\"{Binding SearchText", StringComparison.Ordinal);
        Assert.True(searchTextIndex >= 0);

        var textBoxStart = xaml.LastIndexOf("<TextBox", searchTextIndex, StringComparison.Ordinal);
        Assert.True(textBoxStart >= 0);

        var textBoxEnd = xaml.IndexOf("/>", searchTextIndex, StringComparison.Ordinal);
        Assert.True(textBoxEnd >= 0);

        return xaml[textBoxStart..textBoxEnd];
    }

    private static string ExtractMethodBlock(string code, string methodSignature)
    {
        var methodStart = code.IndexOf(methodSignature, StringComparison.Ordinal);
        Assert.True(methodStart >= 0);

        var nextMethodStart = code.IndexOf("\n    private ", methodStart + methodSignature.Length, StringComparison.Ordinal);
        Assert.True(nextMethodStart >= 0);

        return code[methodStart..nextMethodStart];
    }
}
