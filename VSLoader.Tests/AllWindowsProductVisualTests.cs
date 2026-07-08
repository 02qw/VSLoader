namespace VSLoader.Tests;

public sealed class AllWindowsProductVisualTests
{
    [Theory]
    [InlineData("SettingsWindow.xaml", "ModernGroupBoxStyle", "ModernTextBoxStyle", "SaveCommand", "CancelCommand")]
    [InlineData("WorkspaceSelectorWindow.xaml", "ModernContextMenuStyle", "ModernMenuItemStyle", "OpenSelectedWorkspaceCommand", "StartDeleteWorkspaceCommand")]
    [InlineData("WorkspaceNameDialog.xaml", "ModernDialogSurfaceStyle", "ModernTextBoxStyle", "CreateCommand", "CancelCommand")]
    [InlineData("ShortcutEditWindow.xaml", "ModernDialogSurfaceStyle", "ModernMultilineTextBoxStyle", "SaveCommand", "CancelCommand")]
    [InlineData("BatchImportWindow.xaml", "ModernSectionBorderStyle", "ModernDataGridStyle", "ScanPreviewCommand", "ConfirmImportCommand")]
    [InlineData("MessageDialogWindow.xaml", "ModernDialogSurfaceStyle", "ModernDialogPrimaryButtonStyle", "YesButton_Click", "OkButton_Click")]
    [InlineData("FactoryMapWindow.xaml", "ModernToolStripStyle", "ModernContextMenuStyle", "DownloadAdminUiLinksButton_Click", "EditModeButton_Click")]
    public void Main_application_windows_use_product_visual_resources_and_keep_actions(
        string fileName,
        string expectedStyle1,
        string expectedStyle2,
        string action1,
        string action2)
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            fileName));

        Assert.Contains(expectedStyle1, xaml);
        Assert.Contains(expectedStyle2, xaml);
        Assert.Contains(action1, xaml);
        Assert.Contains(action2, xaml);
    }

    [Fact]
    public void Updater_uses_product_visual_resources_and_keeps_update_feedback_bindings()
    {
        var appXaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader.Updater",
            "App.xaml"));
        var windowXaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader.Updater",
            "MainWindow.xaml"));
        var completedDialogXaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader.Updater",
            "UpdateCompletedDialog.xaml"));

        Assert.Contains("ModernUpdaterTheme.xaml", appXaml);
        Assert.Contains("ModernProgressBarStyle", windowXaml);
        Assert.Contains("ModernLogListBoxStyle", windowXaml);
        Assert.Contains("ReleaseNotesText", windowXaml);
        Assert.Contains("ProgressValue", windowXaml);
        Assert.Contains("DetailLines", windowXaml);
        Assert.Contains("ModernDialogSurfaceStyle", completedDialogXaml);
        Assert.Contains("ReleaseNotesText", completedDialogXaml);
        Assert.Contains("ConfirmButton_Click", completedDialogXaml);
    }
}
