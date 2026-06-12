using System.Windows;

namespace VSLoader.Models;

internal static class FactoryMapViewportFitCalculator
{
    public static FactoryMapViewportFitResult Calculate(
        double viewportWidth,
        double viewportHeight,
        Rect contentBounds,
        double padding)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0 || contentBounds.Width <= 0 || contentBounds.Height <= 0)
        {
            return new FactoryMapViewportFitResult(1.0, 0, 0);
        }

        var availableWidth = Math.Max(1, viewportWidth - padding * 2);
        var availableHeight = Math.Max(1, viewportHeight - padding * 2);
        var scaleX = availableWidth / contentBounds.Width;
        var scaleY = availableHeight / contentBounds.Height;
        var scale = Math.Min(1.0, Math.Min(scaleX, scaleY));
        var scaledWidth = contentBounds.Width * scale;
        var scaledHeight = contentBounds.Height * scale;
        var offsetX = (viewportWidth - scaledWidth) / 2 - contentBounds.Left * scale;
        var offsetY = (viewportHeight - scaledHeight) / 2 - contentBounds.Top * scale;

        return new FactoryMapViewportFitResult(scale, offsetX, offsetY);
    }
}
