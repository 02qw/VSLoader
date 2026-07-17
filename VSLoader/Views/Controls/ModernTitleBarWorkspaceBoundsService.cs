using System.Windows;

namespace VSLoader.Views.Controls;

internal static class ModernTitleBarWorkspaceBoundsService
{
    public static Rect NormalizeRestoreBounds(
        Rect restoreBounds,
        Rect workArea,
        double minWidth,
        double minHeight)
    {
        var width = NormalizeLength(restoreBounds.Width, minWidth, workArea.Width);
        var height = NormalizeLength(restoreBounds.Height, minHeight, workArea.Height);
        if (IsEffectivelyWorkspaceSized(width, height, workArea))
        {
            width = NormalizeLength(workArea.Width * 0.7, minWidth, workArea.Width);
            height = NormalizeLength(workArea.Height * 0.7, minHeight, workArea.Height);
            return CenterInWorkArea(workArea, width, height);
        }

        var normalized = new Rect(restoreBounds.Left, restoreBounds.Top, width, height);

        if (!workArea.IntersectsWith(normalized))
        {
            normalized = CenterInWorkArea(workArea, width, height);
        }

        if (normalized.Right > workArea.Right)
        {
            normalized.X = Math.Max(workArea.Left, workArea.Right - normalized.Width);
        }

        if (normalized.Bottom > workArea.Bottom)
        {
            normalized.Y = Math.Max(workArea.Top, workArea.Bottom - normalized.Height);
        }

        if (normalized.Left < workArea.Left)
        {
            normalized.X = workArea.Left;
        }

        if (normalized.Top < workArea.Top)
        {
            normalized.Y = workArea.Top;
        }

        return normalized;
    }

    private static bool IsEffectivelyWorkspaceSized(double width, double height, Rect workArea)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return false;
        }

        return width >= workArea.Width * 0.96 && height >= workArea.Height * 0.96;
    }

    private static Rect CenterInWorkArea(Rect workArea, double width, double height)
    {
        return new Rect(
            workArea.Left + Math.Max(0, (workArea.Width - width) / 2),
            workArea.Top + Math.Max(0, (workArea.Height - height) / 2),
            width,
            height);
    }

    private static double NormalizeLength(double value, double minimum, double maximum)
    {
        var effectiveMinimum = double.IsFinite(minimum) && minimum > 0 ? minimum : 1;
        var effectiveMaximum = double.IsFinite(maximum) && maximum > 0 ? maximum : effectiveMinimum;
        var effectiveValue = double.IsFinite(value) && value > 0 ? value : effectiveMinimum;
        return Math.Min(Math.Max(effectiveValue, effectiveMinimum), effectiveMaximum);
    }
}
