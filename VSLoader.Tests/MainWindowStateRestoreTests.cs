using VSLoader.Models;

namespace VSLoader.Tests;

public sealed class MainWindowStateRestoreTests
{
    [Theory]
    [InlineData(null, MainWindowStateKinds.Normal)]
    [InlineData("", MainWindowStateKinds.Normal)]
    [InlineData("invalid", MainWindowStateKinds.Normal)]
    [InlineData(MainWindowStateKinds.Normal, MainWindowStateKinds.Normal)]
    [InlineData(MainWindowStateKinds.WorkspaceMaximized, MainWindowStateKinds.WorkspaceMaximized)]
    public void NormalizeMainWindowState_accepts_only_supported_launch_states(string? state, string expected)
    {
        Assert.Equal(expected, MainWindow.NormalizeMainWindowState(state));
    }

    [Fact]
    public void Main_window_layout_snapshot_saves_and_restores_presentation_state()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.Contains("MainWindowState = _runtimeLayoutState.MainWindowState", code);
        Assert.Contains("_runtimeLayoutState.MainWindowState = NormalizeMainWindowState(config.MainWindowState)", code);
        Assert.Contains("RestoreMainWindowPresentationState();", code);
        Assert.Contains("ModernTitleBar.ApplyWorkspaceMaximized(this);", code);
    }

    [Fact]
    public void Main_window_close_paths_save_presentation_state_immediately()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        var closingStart = code.IndexOf("private void MainWindow_Closing", StringComparison.Ordinal);
        var closingEnd = code.IndexOf("private void ToggleWindowFromHotkey", closingStart, StringComparison.Ordinal);
        Assert.True(closingStart >= 0);
        Assert.True(closingEnd > closingStart);
        var closingBlock = code[closingStart..closingEnd];
        Assert.Contains("SaveMainWindowPresentationStateToSession();", closingBlock);
        Assert.Contains("SaveWindowLayoutImmediately();", closingBlock);

        var cleanupStart = code.IndexOf("private void CleanupForClose", StringComparison.Ordinal);
        var cleanupEnd = code.IndexOf("private void RequestRealApplicationExit", cleanupStart, StringComparison.Ordinal);
        Assert.True(cleanupStart >= 0);
        Assert.True(cleanupEnd > cleanupStart);
        var cleanupBlock = code[cleanupStart..cleanupEnd];
        Assert.Contains("SaveMainWindowPresentationStateToSession();", cleanupBlock);
        Assert.Contains("SaveWindowLayoutImmediately();", cleanupBlock);
    }

    [Fact]
    public void Main_title_bar_notifies_owner_after_maximize_or_restore_finishes()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml"));
        var titleBarCode = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "Controls",
            "ModernTitleBar.xaml.cs"));

        Assert.Contains("WorkspaceMaximizedChanged=\"MainTitleBar_WorkspaceMaximizedChanged\"", xaml);
        Assert.Contains("public event EventHandler? WorkspaceMaximizedChanged;", titleBarCode);
        Assert.Contains("WorkspaceMaximizedChanged?.Invoke(this, EventArgs.Empty);", titleBarCode);
    }
}
