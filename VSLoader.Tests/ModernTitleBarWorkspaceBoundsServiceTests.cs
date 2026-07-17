using System.Windows;
using VSLoader.Views.Controls;

namespace VSLoader.Tests;

public sealed class ModernTitleBarWorkspaceBoundsServiceTests
{
    [Fact]
    public void Normalize_restore_bounds_keeps_visible_bounds_when_inside_work_area()
    {
        var restoreBounds = new Rect(120, 80, 900, 640);
        var workArea = new Rect(0, 0, 1920, 1040);

        var normalized = ModernTitleBarWorkspaceBoundsService.NormalizeRestoreBounds(
            restoreBounds,
            workArea,
            minWidth: 320,
            minHeight: 240);

        Assert.Equal(restoreBounds, normalized);
    }

    [Fact]
    public void Normalize_restore_bounds_centers_window_when_saved_bounds_are_off_screen()
    {
        var restoreBounds = new Rect(-2000, -1600, 900, 640);
        var workArea = new Rect(0, 0, 1920, 1040);

        var normalized = ModernTitleBarWorkspaceBoundsService.NormalizeRestoreBounds(
            restoreBounds,
            workArea,
            minWidth: 320,
            minHeight: 240);

        Assert.Equal(510, normalized.Left);
        Assert.Equal(200, normalized.Top);
        Assert.Equal(900, normalized.Width);
        Assert.Equal(640, normalized.Height);
    }

    [Fact]
    public void Normalize_restore_bounds_respects_minimum_and_work_area_size()
    {
        var restoreBounds = new Rect(10, 20, 80, 90);
        var workArea = new Rect(0, 0, 800, 600);

        var normalized = ModernTitleBarWorkspaceBoundsService.NormalizeRestoreBounds(
            restoreBounds,
            workArea,
            minWidth: 320,
            minHeight: 240);

        Assert.Equal(320, normalized.Width);
        Assert.Equal(240, normalized.Height);
        Assert.True(workArea.IntersectsWith(normalized));
    }

    [Fact]
    public void Normalize_restore_bounds_replaces_workspace_sized_bounds_with_centered_window()
    {
        var restoreBounds = new Rect(0, 0, 1900, 1030);
        var workArea = new Rect(0, 0, 1920, 1040);

        var normalized = ModernTitleBarWorkspaceBoundsService.NormalizeRestoreBounds(
            restoreBounds,
            workArea,
            minWidth: 320,
            minHeight: 240);

        Assert.Equal(1344, normalized.Width);
        Assert.Equal(728, normalized.Height);
        Assert.Equal(288, normalized.Left);
        Assert.Equal(156, normalized.Top);
    }

    [Fact]
    public void Modern_title_bar_marks_workspace_maximized_before_applying_workspace_size()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "Controls",
            "ModernTitleBar.xaml.cs"));

        var maximizeStart = code.IndexOf("public static void ApplyWorkspaceMaximized", StringComparison.Ordinal);
        var restoreStart = code.IndexOf("private static void SetIsWorkspaceMaximized", StringComparison.Ordinal);
        Assert.True(maximizeStart >= 0);
        Assert.True(restoreStart > maximizeStart);

        var maximizeBlock = code[maximizeStart..restoreStart];
        var stateIndex = maximizeBlock.IndexOf("SetIsWorkspaceMaximized(window, true);", StringComparison.Ordinal);
        var leftIndex = maximizeBlock.IndexOf("window.Left = workArea.Left;", StringComparison.Ordinal);
        var widthIndex = maximizeBlock.IndexOf("window.Width = workArea.Width;", StringComparison.Ordinal);

        Assert.True(stateIndex >= 0);
        Assert.True(leftIndex > stateIndex);
        Assert.True(widthIndex > stateIndex);
    }
}
