using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfVector = System.Windows.Vector;

namespace VSLoader.Models;

internal static class FactoryMapEditMath
{
    public static double SnapToGrid(double value, double gridSize)
    {
        if (gridSize <= 0)
        {
            return value;
        }

        return Math.Round(value / gridSize, MidpointRounding.AwayFromZero) * gridSize;
    }

    public static double ClampAndSnapToGrid(double value, double gridSize)
    {
        return Math.Max(0, SnapToGrid(value, gridSize));
    }

    public static bool RectIntersects(WpfRect first, WpfRect second)
    {
        return first.IntersectsWith(second);
    }

    public static Dictionary<string, WpfPoint> ApplySnappedDelta(
        IReadOnlyDictionary<string, WpfPoint> startPositions,
        WpfVector delta,
        double gridSize)
    {
        return startPositions.ToDictionary(
            pair => pair.Key,
            pair => new WpfPoint(
                ClampAndSnapToGrid(pair.Value.X + delta.X, gridSize),
                ClampAndSnapToGrid(pair.Value.Y + delta.Y, gridSize)));
    }
}
