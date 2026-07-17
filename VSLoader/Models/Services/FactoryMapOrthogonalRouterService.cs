using System.Globalization;
using VSLoader.Models;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfVector = System.Windows.Vector;

namespace VSLoader.Services;

public sealed class FactoryMapOrthogonalRouterService
{
    private const double Epsilon = 0.001;

    public FactoryMapRouteResult Route(
        FactoryMapDeviceViewData map,
        FactoryMapConnectionPoint from,
        FactoryMapConnectionPoint to,
        double gridSize)
    {
        if (map is null
            || from is null
            || to is null
            || !double.IsFinite(gridSize)
            || gridSize <= 0
            || !IsFinite(from)
            || !IsFinite(to))
        {
            return FactoryMapRouteResult.Failed("线路路由参数无效。");
        }

        if (IsSamePoint(from.X, from.Y, to.X, to.Y))
        {
            return FactoryMapRouteResult.Failed("两个连接点坐标重合，无法创建零长度线路。");
        }

        string? endpointError = null;
        var describedAnyClearance = false;
        var preferredClearance = Math.Max(gridSize * 2, 20d);
        foreach (var clearance in new[] { preferredClearance, gridSize }.Distinct())
        {
            if (!TryDescribeEndpoint(map, from, gridSize, clearance, out var start, out var startError))
            {
                endpointError = startError;
                continue;
            }

            if (!TryDescribeEndpoint(map, to, gridSize, clearance, out var end, out var endError))
            {
                endpointError = endError;
                continue;
            }

            describedAnyClearance = true;
            var candidates = BuildCandidates(map, start, end, gridSize, clearance)
                .Select(Normalize)
                .Where(points => IsValidRoute(map, points, start, end, clearance))
                .GroupBy(CreateCanonicalKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(points => Math.Max(0, points.Count - 2))
                .ThenBy(CalculateLength)
                .ThenBy(CreateCanonicalKey, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length > 0)
            {
                return FactoryMapRouteResult.Succeeded(candidates[0].Select(point =>
                    new FactoryMapPoint { X = point.X, Y = point.Y }));
            }
        }

        return FactoryMapRouteResult.Failed(describedAnyClearance
            ? "无法在节点外部生成合法的正交线路，请调整节点位置后重试。"
            : endpointError ?? "线路端点无法沿指定方向安全出线。");
    }

    private static bool TryDescribeEndpoint(
        FactoryMapDeviceViewData map,
        FactoryMapConnectionPoint point,
        double gridSize,
        double clearance,
        out EndpointDescriptor descriptor,
        out string errorMessage)
    {
        var position = new WpfPoint(point.X, point.Y);
        if (point.Kind != FactoryMapConnectionPointKinds.Attached)
        {
            descriptor = new EndpointDescriptor(point, position, position, default, null);
            errorMessage = string.Empty;
            return true;
        }

        var owner = map.Devices.FirstOrDefault(device =>
            string.Equals(device.Id, point.OwnerNodeId, StringComparison.OrdinalIgnoreCase));
        if (owner is null)
        {
            descriptor = default!;
            errorMessage = $"连接点 {point.Id} 的所属节点不存在。";
            return false;
        }

        if (!FactoryMapEndpointGeometryService.TryGetOutwardDirection(point.Side, out var direction))
        {
            descriptor = default!;
            errorMessage = $"连接点 {point.Id} 的端口方向无效。";
            return false;
        }

        var rawEscape = position + (direction * clearance);
        var escape = new WpfPoint(
            Math.Abs(direction.X) > Epsilon
                ? SnapOutward(rawEscape.X, gridSize, lower: direction.X < 0)
                : rawEscape.X,
            Math.Abs(direction.Y) > Epsilon
                ? SnapOutward(rawEscape.Y, gridSize, lower: direction.Y < 0)
                : rawEscape.Y);
        if (escape.X < 0 || escape.Y < 0)
        {
            descriptor = default!;
            errorMessage = $"连接点 {point.Id} 距离地图边界不足，无法沿端口方向安全出线。";
            return false;
        }

        descriptor = new EndpointDescriptor(point, position, escape, direction, owner.Id);
        errorMessage = string.Empty;
        return true;
    }

    private static IEnumerable<List<WpfPoint>> BuildCandidates(
        FactoryMapDeviceViewData map,
        EndpointDescriptor start,
        EndpointDescriptor end,
        double gridSize,
        double clearance)
    {
        var first = start.Escape;
        var last = end.Escape;
        yield return Complete(start, end, [first, last]);
        yield return Complete(start, end, [first, new WpfPoint(last.X, first.Y), last]);
        yield return Complete(start, end, [first, new WpfPoint(first.X, last.Y), last]);

        var middleX = Snap((first.X + last.X) / 2, gridSize);
        var middleY = Snap((first.Y + last.Y) / 2, gridSize);
        yield return Complete(start, end,
            [first, new WpfPoint(middleX, first.Y), new WpfPoint(middleX, last.Y), last]);
        yield return Complete(start, end,
            [first, new WpfPoint(first.X, middleY), new WpfPoint(last.X, middleY), last]);

        if (map.Devices.Count == 0)
        {
            yield break;
        }

        var left = SnapOutward(map.Devices.Min(device => device.X - clearance) - clearance, gridSize, lower: true);
        var right = SnapOutward(map.Devices.Max(device => device.X + FactoryMapNodeGeometryService.GetWidth(device) + clearance) + clearance, gridSize, lower: false);
        var top = SnapOutward(map.Devices.Min(device => device.Y - clearance) - clearance, gridSize, lower: true);
        var bottom = SnapOutward(map.Devices.Max(device => device.Y + FactoryMapNodeGeometryService.GetHeight(device) + clearance) + clearance, gridSize, lower: false);
        if (left >= 0)
        {
            yield return Complete(start, end,
                [first, new WpfPoint(left, first.Y), new WpfPoint(left, last.Y), last]);
        }

        yield return Complete(start, end,
            [first, new WpfPoint(right, first.Y), new WpfPoint(right, last.Y), last]);
        if (top >= 0)
        {
            yield return Complete(start, end,
                [first, new WpfPoint(first.X, top), new WpfPoint(last.X, top), last]);
        }

        yield return Complete(start, end,
            [first, new WpfPoint(first.X, bottom), new WpfPoint(last.X, bottom), last]);
    }

    private static List<WpfPoint> Complete(
        EndpointDescriptor start,
        EndpointDescriptor end,
        IReadOnlyList<WpfPoint> middle)
    {
        var result = new List<WpfPoint> { start.Position };
        result.AddRange(middle);
        result.Add(end.Position);
        return result;
    }

    private static List<WpfPoint> Normalize(IReadOnlyList<WpfPoint> raw)
    {
        var result = new List<WpfPoint>();
        foreach (var point in raw)
        {
            if (result.Count > 0 && IsSamePoint(result[^1], point))
            {
                continue;
            }

            result.Add(point);
            while (result.Count >= 3 && IsCollinear(result[^3], result[^2], result[^1]))
            {
                result.RemoveAt(result.Count - 2);
            }
        }

        return result;
    }

    private static bool IsValidRoute(
        FactoryMapDeviceViewData map,
        IReadOnlyList<WpfPoint> points,
        EndpointDescriptor start,
        EndpointDescriptor end,
        double clearance)
    {
        if (points.Count < 2
            || !IsSamePoint(points[0], start.Position)
            || !IsSamePoint(points[^1], end.Position))
        {
            return false;
        }

        if (start.IsAttached && !HasDirection(points[0], points[1], start.Direction))
        {
            return false;
        }

        if (end.IsAttached && !HasDirection(points[^2], points[^1], -end.Direction))
        {
            return false;
        }

        for (var segmentIndex = 0; segmentIndex < points.Count - 1; segmentIndex++)
        {
            var segmentStart = points[segmentIndex];
            var segmentEnd = points[segmentIndex + 1];
            if (!IsFinite(segmentStart)
                || !IsFinite(segmentEnd)
                || segmentStart.X < 0
                || segmentStart.Y < 0
                || segmentEnd.X < 0
                || segmentEnd.Y < 0
                || IsSamePoint(segmentStart, segmentEnd)
                || !IsOrthogonal(segmentStart, segmentEnd))
            {
                return false;
            }

            foreach (var device in map.Devices)
            {
                var isStartStub = segmentIndex == 0
                    && string.Equals(device.Id, start.OwnerNodeId, StringComparison.OrdinalIgnoreCase);
                var isEndStub = segmentIndex == points.Count - 2
                    && string.Equals(device.Id, end.OwnerNodeId, StringComparison.OrdinalIgnoreCase);
                if (isStartStub || isEndStub)
                {
                    continue;
                }

                var nodeRect = new WpfRect(
                    device.X,
                    device.Y,
                    FactoryMapNodeGeometryService.GetWidth(device),
                    FactoryMapNodeGeometryService.GetHeight(device));
                if (IntersectsInteriorOrOverlapsBoundary(segmentStart, segmentEnd, nodeRect))
                {
                    return false;
                }

                var expanded = new WpfRect(
                    device.X - clearance,
                    device.Y - clearance,
                    FactoryMapNodeGeometryService.GetWidth(device) + (clearance * 2),
                    FactoryMapNodeGeometryService.GetHeight(device) + (clearance * 2));
                if (IntersectsInterior(segmentStart, segmentEnd, expanded))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IntersectsInteriorOrOverlapsBoundary(WpfPoint start, WpfPoint end, WpfRect rect)
    {
        if (NearlyEqual(start.Y, end.Y))
        {
            var overlap = OverlapLength(start.X, end.X, rect.Left, rect.Right);
            return overlap > Epsilon
                && ((start.Y > rect.Top + Epsilon && start.Y < rect.Bottom - Epsilon)
                    || NearlyEqual(start.Y, rect.Top)
                    || NearlyEqual(start.Y, rect.Bottom));
        }

        var verticalOverlap = OverlapLength(start.Y, end.Y, rect.Top, rect.Bottom);
        return verticalOverlap > Epsilon
            && ((start.X > rect.Left + Epsilon && start.X < rect.Right - Epsilon)
                || NearlyEqual(start.X, rect.Left)
                || NearlyEqual(start.X, rect.Right));
    }

    private static bool IntersectsInterior(WpfPoint start, WpfPoint end, WpfRect rect)
    {
        if (NearlyEqual(start.Y, end.Y))
        {
            return start.Y > rect.Top + Epsilon
                && start.Y < rect.Bottom - Epsilon
                && OverlapLength(start.X, end.X, rect.Left, rect.Right) > Epsilon;
        }

        return start.X > rect.Left + Epsilon
            && start.X < rect.Right - Epsilon
            && OverlapLength(start.Y, end.Y, rect.Top, rect.Bottom) > Epsilon;
    }

    private static bool HasDirection(WpfPoint from, WpfPoint to, WpfVector expected)
    {
        var delta = to - from;
        if (Math.Abs(expected.X) > Epsilon)
        {
            return NearlyEqual(delta.Y, 0) && Math.Sign(delta.X) == Math.Sign(expected.X);
        }

        return NearlyEqual(delta.X, 0) && Math.Sign(delta.Y) == Math.Sign(expected.Y);
    }

    private static double CalculateLength(IReadOnlyList<WpfPoint> points)
    {
        var result = 0d;
        for (var index = 0; index < points.Count - 1; index++)
        {
            result += Math.Abs(points[index + 1].X - points[index].X)
                + Math.Abs(points[index + 1].Y - points[index].Y);
        }

        return result;
    }

    private static string CreateCanonicalKey(IReadOnlyList<WpfPoint> points)
    {
        var forward = string.Join(";", points.Select(PointKey));
        var reverse = string.Join(";", points.Reverse().Select(PointKey));
        return string.CompareOrdinal(forward, reverse) <= 0 ? forward : reverse;
    }

    private static string PointKey(WpfPoint point)
    {
        return $"{point.X.ToString("R", CultureInfo.InvariantCulture)},{point.Y.ToString("R", CultureInfo.InvariantCulture)}";
    }

    private static double OverlapLength(double firstStart, double firstEnd, double secondStart, double secondEnd)
    {
        return Math.Max(0, Math.Min(Math.Max(firstStart, firstEnd), Math.Max(secondStart, secondEnd))
            - Math.Max(Math.Min(firstStart, firstEnd), Math.Min(secondStart, secondEnd)));
    }

    private static double Snap(double value, double gridSize)
    {
        return Math.Round(value / gridSize, MidpointRounding.AwayFromZero) * gridSize;
    }

    private static double SnapOutward(double value, double gridSize, bool lower)
    {
        return (lower ? Math.Floor(value / gridSize) : Math.Ceiling(value / gridSize)) * gridSize;
    }

    private static bool IsFinite(FactoryMapConnectionPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    private static bool IsFinite(WpfPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    private static bool IsOrthogonal(WpfPoint first, WpfPoint second)
    {
        return NearlyEqual(first.X, second.X) || NearlyEqual(first.Y, second.Y);
    }

    private static bool IsCollinear(WpfPoint first, WpfPoint second, WpfPoint third)
    {
        return (NearlyEqual(first.X, second.X) && NearlyEqual(second.X, third.X))
            || (NearlyEqual(first.Y, second.Y) && NearlyEqual(second.Y, third.Y));
    }

    private static bool IsSamePoint(WpfPoint first, WpfPoint second)
    {
        return IsSamePoint(first.X, first.Y, second.X, second.Y);
    }

    private static bool IsSamePoint(double firstX, double firstY, double secondX, double secondY)
    {
        return NearlyEqual(firstX, secondX) && NearlyEqual(firstY, secondY);
    }

    private static bool NearlyEqual(double first, double second) => Math.Abs(first - second) < Epsilon;

    private sealed record EndpointDescriptor(
        FactoryMapConnectionPoint Point,
        WpfPoint Position,
        WpfPoint Escape,
        WpfVector Direction,
        string? OwnerNodeId)
    {
        public bool IsAttached => Point.Kind == FactoryMapConnectionPointKinds.Attached;
    }
}
