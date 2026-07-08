namespace VSLoader.Tests;

public sealed class FactoryMapWindowInputMethodTests
{
    [Fact]
    public void Factory_map_window_disables_input_method_to_prevent_letter_hotkey_from_starting_ime_composition()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml"));

        Assert.Contains("InputMethod.IsInputMethodEnabled=\"False\"", xaml);
    }
}
