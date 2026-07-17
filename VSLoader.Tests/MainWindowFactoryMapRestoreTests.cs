namespace VSLoader.Tests;

public sealed class MainWindowFactoryMapRestoreTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ShouldRestoreFactoryMapBounds_depends_on_session_bounds_not_main_window_state(
        bool useSessionBounds,
        bool hasFactoryMapBounds,
        bool expected)
    {
        Assert.Equal(expected, MainWindow.ShouldRestoreFactoryMapBounds(useSessionBounds, hasFactoryMapBounds));
    }

    [Fact]
    public void PositionFactoryMapWindow_does_not_skip_session_bounds_when_main_window_is_minimized()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.DoesNotContain("_factoryMapWindow is null || WindowState == WindowState.Minimized", code);
        Assert.Contains("ShouldRestoreFactoryMapBounds", code);
        Assert.Contains("CalculateDefaultFactoryMapBoundsWithoutMainWindow", code);
    }

    [Fact]
    public void Factory_map_user_close_saves_layout_immediately_and_closed_handler_only_cleans_up()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        var closeMethodStart = code.IndexOf("private void CloseFactoryMapByUserAction()", StringComparison.Ordinal);
        var closeMethodEnd = code.IndexOf("private void CloseFactoryMapForExit()", StringComparison.Ordinal);
        Assert.True(closeMethodStart >= 0);
        Assert.True(closeMethodEnd > closeMethodStart);
        var closeMethod = code[closeMethodStart..closeMethodEnd];
        Assert.Contains("SaveFactoryMapStateToSession", closeMethod);
        Assert.Contains("SaveWindowLayoutImmediately", closeMethod);

        var closedHandlerStart = code.IndexOf("_factoryMapWindow.Closed += (_, _) =>", StringComparison.Ordinal);
        var closedHandlerEnd = code.IndexOf("PositionFactoryMapWindow(useSessionBounds: true)", StringComparison.Ordinal);
        Assert.True(closedHandlerStart >= 0);
        Assert.True(closedHandlerEnd > closedHandlerStart);
        var closedHandler = code[closedHandlerStart..closedHandlerEnd];
        Assert.DoesNotContain("SaveFactoryMapStateToSession", closedHandler);
    }

    [Fact]
    public void Factory_map_window_state_is_saved_and_restored_with_layout()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.Contains("FactoryMapWindowState = _runtimeLayoutState.FactoryMapWindowState", code);
        Assert.Contains("_runtimeLayoutState.FactoryMapWindowState = NormalizeFactoryMapWindowState", code);
        Assert.Contains("RestoreFactoryMapWindowState();", code);
        Assert.Contains("ModernTitleBar.ApplyWorkspaceMaximized(_factoryMapWindow);", code);
        Assert.Contains("FactoryMapWindowStateKinds.Minimized", code);
        Assert.Contains("FactoryMapWindowStateKinds.WorkspaceMaximized", code);
    }

    [Fact]
    public void Factory_map_workspace_maximized_state_has_bounds_fallback_for_legacy_layouts()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.Contains("IsFactoryMapEffectivelyWorkspaceMaximized", code);
        Assert.Contains("IsBoundsEffectivelyWorkspaceMaximized", code);
        Assert.Contains("FactoryMapWindowStateKinds.WorkspaceMaximized", code);
        Assert.Contains("config.FactoryMapWindow", code);
    }

    [Fact]
    public void Factory_map_exit_path_refreshes_map_state_before_immediate_layout_save()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        var methodStart = code.IndexOf("private void CloseFactoryMapForExit()", StringComparison.Ordinal);
        var methodEnd = code.IndexOf("private void MainWindow_LocationOrSizeChanged", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);

        var method = code[methodStart..methodEnd];
        var saveStateIndex = method.IndexOf("SaveFactoryMapStateToSession", StringComparison.Ordinal);
        var saveLayoutIndex = method.IndexOf("SaveWindowLayoutImmediately", StringComparison.Ordinal);
        Assert.True(saveStateIndex >= 0);
        Assert.True(saveLayoutIndex > saveStateIndex);
    }

    [Fact]
    public void Factory_map_button_open_activates_map_window_for_immediate_wheel_input()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        var methodStart = code.IndexOf("private void ToggleFactoryMapWindow()", StringComparison.Ordinal);
        var methodEnd = code.IndexOf("private void ShowFactoryMapIfNeeded()", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);

        var method = code[methodStart..methodEnd];
        var showIndex = method.LastIndexOf("ShowFactoryMapIfNeeded();", StringComparison.Ordinal);
        var activateIndex = method.LastIndexOf("ActivateFactoryMapWindow();", StringComparison.Ordinal);
        Assert.True(showIndex >= 0);
        Assert.True(activateIndex > showIndex);
    }

    [Fact]
    public void Factory_map_hotkey_activation_does_not_reload_map_or_reset_selection_state()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        var hotkeyMethodStart = code.IndexOf(
            "private void ToggleFactoryMapFromGlobalHotkey",
            StringComparison.Ordinal);
        var blockedMethodStart = code.IndexOf(
            "private bool IsFactoryMapHotkeyBlocked",
            hotkeyMethodStart,
            StringComparison.Ordinal);
        Assert.True(hotkeyMethodStart >= 0);
        Assert.True(blockedMethodStart > hotkeyMethodStart);

        var hotkeyMethod = code[hotkeyMethodStart..blockedMethodStart];
        var activateCaseStart = hotkeyMethod.IndexOf(
            "case FactoryMapHotkeyAction.Activate:",
            StringComparison.Ordinal);
        var minimizeCaseStart = hotkeyMethod.IndexOf(
            "case FactoryMapHotkeyAction.Minimize:",
            activateCaseStart,
            StringComparison.Ordinal);
        Assert.True(activateCaseStart >= 0);
        Assert.True(minimizeCaseStart > activateCaseStart);
        Assert.DoesNotContain(
            "RefreshFactoryMap();",
            hotkeyMethod[activateCaseStart..minimizeCaseStart],
            StringComparison.Ordinal);

        var restoreMethodStart = code.IndexOf(
            "private void RestoreMinimizedFactoryMapWindow",
            StringComparison.Ordinal);
        var restoreStateMethodStart = code.IndexOf(
            "private void RestoreFactoryMapWindowState",
            restoreMethodStart,
            StringComparison.Ordinal);
        Assert.True(restoreMethodStart >= 0);
        Assert.True(restoreStateMethodStart > restoreMethodStart);
        Assert.DoesNotContain(
            "RefreshFactoryMap();",
            code[restoreMethodStart..restoreStateMethodStart],
            StringComparison.Ordinal);
    }

    [Fact]
    public void Main_window_activation_cancels_pending_factory_map_focus_restore()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        var activatedMethodStart = code.IndexOf(
            "private void MainWindow_Activated",
            StringComparison.Ordinal);
        var deactivatedMethodStart = code.IndexOf(
            "private void MainWindow_Deactivated",
            activatedMethodStart,
            StringComparison.Ordinal);

        Assert.True(activatedMethodStart >= 0);
        Assert.True(deactivatedMethodStart > activatedMethodStart);

        var activatedMethod = code[activatedMethodStart..deactivatedMethodStart];
        Assert.Contains("_factoryMapWindow?.CancelPendingInputFocusRestore();", activatedMethod);
    }

    [Fact]
    public void Imported_workspace_layout_refreshes_runtime_state_before_it_can_be_saved_again()
    {
        var mainWindowCode = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));
        var viewModelCode = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "ViewModels",
            "MainViewModel.cs"));

        Assert.Contains("public event EventHandler? WorkspaceLayoutImported;", viewModelCode);
        Assert.Contains("WorkspaceLayoutImported?.Invoke(this, EventArgs.Empty);", viewModelCode);
        Assert.Contains("viewModel.WorkspaceLayoutImported += MainViewModel_WorkspaceLayoutImported;", mainWindowCode);
        Assert.Contains("viewModel.WorkspaceLayoutImported -= MainViewModel_WorkspaceLayoutImported;", mainWindowCode);
        Assert.Contains("private void MainViewModel_WorkspaceLayoutImported", mainWindowCode);
        Assert.Contains("LoadWindowLayoutConfig();", mainWindowCode);
        Assert.Contains("RestoreShortcutGridColumnWidths();", mainWindowCode);
        Assert.Contains("_factoryMapWindow.RestoreViewState(_runtimeLayoutState.FactoryMapView);", mainWindowCode);
        Assert.Contains("RestoreFactoryMapWindowState();", mainWindowCode);
    }
}
