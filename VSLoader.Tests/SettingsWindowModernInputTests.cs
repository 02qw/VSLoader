namespace VSLoader.Tests;

public sealed class SettingsWindowModernInputTests
{
    [Fact]
    public void Settings_window_keeps_modern_inputs_at_safe_height_and_preserves_wheel_forwarding()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "SettingsWindow.xaml"));

        Assert.DoesNotContain("Height=\"30\"", xaml);
        Assert.Contains("ModernTextBoxStyle", xaml);
        Assert.DoesNotContain("<PasswordBox", xaml);
        Assert.Contains("Text=\"{Binding AdminUiPassword, UpdateSourceTrigger=PropertyChanged}\"", xaml);
        Assert.Contains("ModernReadOnlyTextBoxStyle", xaml);
        Assert.Contains("SettingsInput_PreviewMouseWheel", xaml);
    }
}
