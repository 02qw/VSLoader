namespace VSLoader.Tests;

public sealed class WorkspaceSelectorWindowVisualTests
{
    [Fact]
    public void Xaml_contains_modern_workspace_selector_palette_and_button_styles()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "WorkspaceSelectorWindow.xaml"));

        Assert.Contains("#F6F9FF", xaml);
        Assert.Contains("#EAF3FF", xaml);
        Assert.Contains("#B8D4FF", xaml);
        Assert.Contains("PrimaryWorkspaceButtonStyle", xaml);
        Assert.Contains("DangerWorkspaceButtonStyle", xaml);
    }
}
