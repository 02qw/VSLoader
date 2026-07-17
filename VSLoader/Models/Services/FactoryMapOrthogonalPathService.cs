using VSLoader.Models;
using WpfPoint = System.Windows.Point;

namespace VSLoader.Services;

internal sealed record FactoryMapEdgeSegment(
    int SegmentIndex,
    WpfPoint Start,
    WpfPoint End,
    FactoryMapEdgeSegmentDirection Direction);

internal enum FactoryMapEdgeSegmentDirection
{
    Horizontal,
    Vertical
}

internal static class FactoryMapOrthogonalPathService
{
    private const double Epsilon = 0.001;

    public static List<FactoryMapPoint> Normalize(
        WpfPoint start,
        IReadOnlyList<FactoryMapPoint> points,
        WpfPoint end)
    {
        var path = new List<WpfPoint> { start };
        foreach (var point in points)
        {
            if (IsFinite(point))
            {
                path.Add(new WpfPoint(point.X, point.Y));
            }
        }

        path.Add(end);
        return ToMiddlePoints(NormalizeFullPath(path));
    }

    public static List<FactoryMapPoint> InsertDetour(
        WpfPoint start,
        IReadOnlyList<FactoryMapPoint> points,
        WpfPoint end,
        WpfPoint clickPoint,
        double gridSize)
    {
        var segmentIndex = FindNearestSegmentIndex(start, points, end, clickPoint);
        return InsertDetourOnSegment(start, points, end, segmentIndex, clickPoint, gridSize);
    }

    public static IReadOnlyList<FactoryMapEdgeSegment> GetSegments(
        WpfPoint start,
        IReadOnlyList<FactoryMapPoint> points,
        WpfPoint end)
    {
        var path = CreateNormalizedFullPath(start, points, end);
        var segments = new List<FactoryMapEdgeSegment>();
        for (var i = 0; i < path.Count - 1; i++)
        {
            var segmentStart = path[i];
            var segmentEnd = path[i + 1];
            if (IsSamePoint(segmentStart, segmentEnd))
            {
                continue;
            }

            if (IsHorizontal(segmentStart, segmentEnd))
            {
                segments.Add(new FactoryMapEdgeSegment(
                    i,
                    segmentStart,
                    segmentEnd,
                    FactoryMapEdgeSegmentDirection.Horizontal));
            }
            else if (IsVertical(segmentStart, segmentEnd))
            {
                segments.Add(new FactoryMapEdgeSegment(
                    i,
                    segmentStart,
                    segmentEnd,
                    FactoryMapEdgeSegmentDirection.Vertical));
            }
        }

        return segments;
    }

    public static int FindNearestSegmentIndex(
        WpfPoint start,
        IReadOnlyList<FactoryMapPoint> points,
        WpfPoint end,
        WpfPoint clickPoint)
    {
        var segments = GetSegments(start, points, end);
        var bestIndex = -1;
        var bestDistance = double.PositiveInfinity;
        foreach (var segment in segments)
        {
            var distance = CalculatePointToSegmentDistanceSquared(clickPoint, segment.Start, segment.End);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = segment.SegmentIndex;
            }
        }

        return bestIndex;
    }

    public static List<FactoryMapPoint> InsertDetourOnSegment(
        WpfPoint start,
        IReadOnlyList<FactoryMapPoint> points,
        WpfPoint end,
        int segmentIndex,
        WpfPoint clickPoint,
        double gridSize)
    {
        var path = CreateNormalizedFullPath(start, points, end);
        if (segmentIndex < 0 || segmentIndex >= path.Count - 1)
        {
            return ToMiddlePoints(path);
        }

        var segmentStart = path[segmentIndex];
        var segmentEnd = path[segmentIndex + 1];
        var snappedClick = new WpfPoint(
            FactoryMapEditMath.ClampAndSnapToGrid(clickPoint.X, gridSize),
            FactoryMapEditMath.ClampAndSnapToGrid(clickPoint.Y, gridSize));
        var offset = Math.Max(gridSize * 2, gridSize);
        var inserted = new List<WpfPoint>(path);

        if (IsVertical(segmentStart, segmentEnd))
        {
            var x = snappedClick.X;
            if (NearlyEqual(x, segmentStart.X))
            {
                var direction = clickPoint.X < segmentStart.X ? -offset : offset;
                x = FactoryMapEditMath.ClampAndSnapToGrid(segmentStart.X + direction, gridSize);
            }

            var y = snappedClick.Y;
            inserted.Insert(segmentIndex + 1, new WpfPoint(segmentStart.X, y));
            inserted.Insert(segmentIndex + 2, new WpfPoint(x, y));
            inserted.Insert(segmentIndex + 3, new WpfPoint(x, segmentEnd.Y));
        }
        else if (IsHorizontal(segmentStart, segmentEnd))
        {
            var x = snappedClick.X;
            var y = snappedClick.Y;
            if (NearlyEqual(y, segmentStart.Y))
            {
                var direction = clickPoint.Y < segmentStart.Y ? -offset : offset;
                y = FactoryMapEditMath.ClampAndSnapToGrid(segmentStart.Y + direction, gridSize);
            }

            inserted.Insert(segmentIndex + 1, new WpfPoint(x, segmentStart.Y));
            inserted.Insert(segmentIndex + 2, new WpfPoint(x, y));
            inserted.Insert(segmentIndex + 3, new WpfPoint(segmentEnd.X, y));
        }

        return ToMiddlePoints(NormalizeFullPath(inserted));
    }

    public static List<FactoryMapPoint> MoveSegment(
        WpfPoint start,
        IReadOnlyList<FactoryMapPoint> points,
        WpfPoint end,
        int segmentIndex,
        WpfPoint targetPoint,
        double gridSize,
        bool snapToGrid)
    {
        var path = CreateNormalizedFullPath(start, points, end);
        if (segmentIndex <= 0 || segmentIndex >= path.Count - 2)
        {
            return ToMiddlePoints(path);
        }

        var segmentStart = path[segmentIndex];
        var segmentEnd = path[segmentIndex + 1];
        if (IsHorizontal(segmentStart, segmentEnd))
        {
            var y = snapToGrid
                ? FactoryMapEditMath.ClampAndSnapToGrid(targetPoint.Y, gridSize)
                : Math.Max(0, targetPoint.Y);
            path[segmentIndex] = new WpfPoint(segmentStart.X, y);
            path[segmentIndex + 1] = new WpfPoint(segmentEnd.X, y);
        }
        else if (IsVertical(segmentStart, segmentEnd))
        {
            var x = snapToGrid
                ? FactoryMapEditMath.ClampAndSnapToGrid(targetPoint.X, gridSize)
                : Math.Max(0, targetPoint.X);
            path[segmentIndex] = new WpfPoint(x, segmentStart.Y);
            path[segmentIndex + 1] = new WpfPoint(x, segmentEnd.Y);
        }

        return ToMiddlePoints(NormalizeFullPath(path));
    }

    public static List<FactoryMapPoint> MovePoint(
        WpfPoint start,
        IReadOnlyList<FactoryMapPoint> points,
        WpfPoint end,
        int pointIndex,
        WpfPoint targetPoint,
        double gridSize,
        bool snapToGrid)
    {
        if (pointIndex < 0 || pointIndex >= points.Count)
        {
            return Normalize(start, points, end);
        }

        var normalized = Normalize(start, points, end);
        if (pointIndex >= normalized.Count)
        {
            return normalized;
        }

        var target = snapToGrid
            ? new WpfPoint(
                FactoryMapEditMath.ClampAndSnapToGrid(targetPoint.X, gridSize),
                FactoryMapEditMath.ClampAndSnapToGrid(targetPoint.Y, gridSize))
            : new WpfPoint(Math.Max(0, targetPoint.X), Math.Max(0, targetPoint.Y));

        var path = CreateFullPath(start, normalized, end);
        var pathIndex = pointIndex + 1;
        var previous = path[pathIndex - 1];
        var current = path[pathIndex];
        var next = path[pathIndex + 1];
        var previousSegmentIsHorizontal = IsHorizontal(previous, current);
        var previousSegmentIsVertical = IsVertical(previous, current);
        var nextSegmentIsHorizontal = IsHorizontal(current, next);
        var nextSegmentIsVertical = IsVertical(current, next);

        if (previousSegmentIsHorizontal && nextSegmentIsVertical)
        {
            path[pathIndex] = new WpfPoint(target.X, previous.Y);
            path[pathIndex + 1] = new WpfPoint(target.X, next.Y);
        }
        else if (previousSegmentIsVertical && nextSegmentIsHorizontal)
        {
            path[pathIndex] = new WpfPoint(previous.X, target.Y);
            path[pathIndex + 1] = new WpfPoint(next.X, target.Y);
        }
        else if (previousSegmentIsHorizontal && nextSegmentIsHorizontal)
        {
            path[pathIndex] = new WpfPoint(target.X, previous.Y);
        }
        else if (previousSegmentIsVertical && nextSegmentIsVertical)
        {
            path[pathIndex] = new WpfPoint(previous.X, target.Y);
        }
        else
        {
            path[pathIndex] = target;
        }

        return ToMiddlePoints(NormalizeFullPath(path));
    }

    private static List<WpfPoint> CreateNormalizedFullPath(
        WpfPoint start,
        IReadOnlyList<FactoryMapPoint> points,
        WpfPoint end)
    {
        return CreateFullPath(start, Normalize(start, points, end), end);
    }

    private static List<WpfPoint> CreateFullPath(
        WpfPoint start,
        IReadOnlyList<FactoryMapPoint> points,
        WpfPoint end)
    {
        var path = new List<WpfPoint> { start };
        path.AddRange(points.Select(point => new WpfPoint(point.X, point.Y)));
        path.Add(end);
        return path;
    }

    private static List<WpfPoint> NormalizeFullPath(IReadOnlyList<WpfPoint> rawPath)
    {
        var orthogonalPath = new List<WpfPoint>();
        foreach (var point in rawPath)
        {
            if (!IsFinite(point))
            {
                continue;
            }

            if (orthogonalPath.Count == 0)
            {
                orthogonalPath.Add(point);
                continue;
            }

            var previous = orthogonalPath[^1];
            if (IsSamePoint(previous, point))
            {
                continue;
            }

            if (IsOrthogonal(previous, point))
            {
                orthogonalPath.Add(point);
                continue;
            }

            var direction = GetLastDirection(orthogonalPath);
            var corner = direction == SegmentDirection.Vertical
                ? new WpfPoint(previous.X, point.Y)
                : new WpfPoint(point.X, previous.Y);
            if (!IsSamePoint(previous, corner))
            {
                orthogonalPath.Add(corner);
            }

            if (!IsSamePoint(corner, point))
            {
                orthogonalPath.Add(point);
            }
        }

        return RemoveCollinearPoints(orthogonalPath);
    }

    private static List<WpfPoint> RemoveCollinearPoints(IReadOnlyList<WpfPoint> path)
    {
        var result = new List<WpfPoint>();
        foreach (var point in path)
        {
            if (result.Count > 0 && IsSamePoint(result[^1], point))
            {
                continue;
            }

            result.Add(point);
            while (result.Count >= 3)
            {
                var first = result[^3];
                var second = result[^2];
                var third = result[^1];
                if ((NearlyEqual(first.X, second.X) && NearlyEqual(second.X, third.X))
                    || (NearlyEqual(first.Y, second.Y) && NearlyEqual(second.Y, third.Y)))
                {
                    result.RemoveAt(result.Count - 2);
                    continue;
                }

                break;
            }
        }

        return result;
    }

    private static List<FactoryMapPoint> ToMiddlePoints(IReadOnlyList<WpfPoint> fullPath)
    {
        if (fullPath.Count <= 2)
        {
            return [];
        }

        return fullPath
            .Skip(1)
            .Take(fullPath.Count - 2)
            .Select(point => new FactoryMapPoint { X = point.X, Y = point.Y })
            .ToList();
    }

    private static int FindNearestSegmentIndex(IReadOnlyList<WpfPoint> path, WpfPoint point)
    {
        var bestIndex = 0;
        var bestDistance = double.PositiveInfinity;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var distance = CalculatePointToSegmentDistanceSquared(point, path[i], path[i + 1]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static double CalculatePointToSegmentDistanceSquared(WpfPoint point, WpfPoint start, WpfPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= 0)
        {
            return CalculateDistanceSquared(point, start);
        }

        var t = (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        var projected = new WpfPoint(start.X + (t * dx), start.Y + (t * dy));
        return CalculateDistanceSquared(point, projected);
    }

    private static double CalculateDistanceSquared(WpfPoint first, WpfPoint second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return (dx * dx) + (dy * dy);
    }

    private static SegmentDirection GetLastDirection(IReadOnlyList<WpfPoint> path)
    {
        if (path.Count < 2)
        {
            return SegmentDirection.Unknown;
        }

        var previous = path[^2];
        var current = path[^1];
        if (IsHorizontal(previous, current))
        {
            return SegmentDirection.Horizontal;
        }

        return IsVertical(previous, current)
            ? SegmentDirection.Vertical
            : SegmentDirection.Unknown;
    }

    private static bool IsOrthogonal(WpfPoint first, WpfPoint second)
    {
        return IsHorizontal(first, second) || IsVertical(first, second);
    }

    private static bool IsHorizontal(WpfPoint first, WpfPoint second)
    {
        return NearlyEqual(first.Y, second.Y);
    }

    private static bool IsVertical(WpfPoint first, WpfPoint second)
    {
        return NearlyEqual(first.X, second.X);
    }

    private static bool IsSamePoint(WpfPoint first, WpfPoint second)
    {
        return NearlyEqual(first.X, second.X) && NearlyEqual(first.Y, second.Y);
    }

    private static bool IsFinite(FactoryMapPoint point)
    {
        return double.IsFinite(point.X) && double.IsFinite(point.Y);
    }

    private static bool IsFinite(WpfPoint point)
    {
        return double.IsFinite(point.X) && double.IsFinite(point.Y);
    }

    private static bool NearlyEqual(double first, double second)
    {
        return Math.Abs(first - second) < Epsilon;
    }

    private enum SegmentDirection
    {
        Unknown,
        Horizontal,
        Vertical
    }
}
