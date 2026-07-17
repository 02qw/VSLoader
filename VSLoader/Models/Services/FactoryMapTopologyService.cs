using VSLoader.Models;

namespace VSLoader.Services;

public sealed class FactoryMapTopologyService
{
    private const double Epsilon = 0.001;
    private readonly FactoryMapOrthogonalRouterService routerService = new();

    public FactoryMapTopologyOperationResult ConnectPoints(
        FactoryMapDeviceViewData map,
        string fromPointId,
        string toPointId,
        double gridSize = 10)
    {
        if (!TryFindPoint(map, fromPointId, out var from)
            || !TryFindPoint(map, toPointId, out var to))
        {
            return FactoryMapTopologyOperationResult.Failed("连接点不存在。请重新选择连接端点。");
        }

        if (string.Equals(from.Id, to.Id, StringComparison.OrdinalIgnoreCase))
        {
            return FactoryMapTopologyOperationResult.Failed("连接点不能连接自己。");
        }

        if (HasPath(map, from.Id, to.Id))
        {
            return FactoryMapTopologyOperationResult.Failed("两个连接点之间已经存在连接。");
        }

        if ((from.Kind == FactoryMapConnectionPointKinds.Bend && GetDegree(map, from.Id) >= 2)
            || (to.Kind == FactoryMapConnectionPointKinds.Bend && GetDegree(map, to.Id) >= 2))
        {
            return FactoryMapTopologyOperationResult.Failed("折弯点不能直接作为分支点，请先转换为普通连接点。");
        }

        var route = routerService.Route(map, from, to, gridSize);
        if (!route.Success)
        {
            return FactoryMapTopologyOperationResult.Failed(route.ErrorMessage ?? "无法生成合法的正交线路。");
        }

        var points = ClonePoints(map.ConnectionPoints);
        var segments = CloneSegments(map.Segments);
        var nextZIndex = segments.Count == 0 ? 0 : segments.Max(segment => segment.ZIndex) + 1;
        var pathPointIds = new List<string> { from.Id };
        foreach (var routePoint in route.Points.Skip(1).Take(Math.Max(0, route.Points.Count - 2)))
        {
            var bend = new FactoryMapConnectionPoint
            {
                Id = CreateUniqueId("bend", points.Select(point => point.Id)),
                Kind = FactoryMapConnectionPointKinds.Bend,
                X = routePoint.X,
                Y = routePoint.Y
            };
            points.Add(bend);
            pathPointIds.Add(bend.Id);
        }

        pathPointIds.Add(to.Id);
        for (var index = 0; index < pathPointIds.Count - 1; index++)
        {
            segments.Add(CreateSegment(pathPointIds[index], pathPointIds[index + 1], nextZIndex + index));
        }

        return CommitIfValid(map, points, segments);
    }

    public FactoryMapTopologyOperationResult DisconnectSegment(
        FactoryMapDeviceViewData map,
        string segmentId)
    {
        var segments = CloneSegments(map.Segments);
        if (segments.RemoveAll(segment => string.Equals(segment.Id, segmentId, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            return FactoryMapTopologyOperationResult.Failed("需要断开的线段不存在。");
        }

        var result = CommitIfValid(map, ClonePoints(map.ConnectionPoints), segments);
        if (result.Success)
        {
            NormalizeJunctions(map);
            CleanupOrphanBendPoints(map);
        }

        return result;
    }

    public FactoryMapTopologyOperationResult DisconnectPoint(
        FactoryMapDeviceViewData map,
        string pointId)
    {
        if (!TryFindPoint(map, pointId, out _))
        {
            return FactoryMapTopologyOperationResult.Failed("连接点不存在。");
        }

        var segments = CloneSegments(map.Segments);
        segments.RemoveAll(segment => ReferencesPoint(segment, pointId));
        var result = CommitIfValid(map, ClonePoints(map.ConnectionPoints), segments);
        if (result.Success)
        {
            NormalizeJunctions(map);
            CleanupOrphanBendPoints(map);
        }

        return result;
    }

    public FactoryMapTopologyOperationResult DisconnectNode(
        FactoryMapDeviceViewData map,
        string nodeId)
    {
        if (!map.Devices.Any(device => string.Equals(device.Id, nodeId, StringComparison.OrdinalIgnoreCase)))
        {
            return FactoryMapTopologyOperationResult.Failed("节点不存在。");
        }

        var attachedIds = map.ConnectionPoints
            .Where(point => point.Kind == FactoryMapConnectionPointKinds.Attached
                && string.Equals(point.OwnerNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
            .Select(point => point.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var segments = CloneSegments(map.Segments);
        segments.RemoveAll(segment => attachedIds.Contains(segment.FromPointId) || attachedIds.Contains(segment.ToPointId));
        var result = CommitIfValid(map, ClonePoints(map.ConnectionPoints), segments);
        if (result.Success)
        {
            NormalizeJunctions(map);
            CleanupOrphanBendPoints(map);
        }

        return result;
    }

    public FactoryMapTopologyOperationResult SplitSegmentAt(
        FactoryMapDeviceViewData map,
        string segmentId,
        double x,
        double y,
        double gridSize,
        double endpointThreshold)
    {
        var segment = map.Segments.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, segmentId, StringComparison.OrdinalIgnoreCase));
        if (segment is null
            || !TryFindPoint(map, segment.FromPointId, out var from)
            || !TryFindPoint(map, segment.ToPointId, out var to))
        {
            return FactoryMapTopologyOperationResult.Failed("需要拆分的线段不存在或端点无效。");
        }

        var projected = ProjectAndSnap(from, to, x, y, gridSize);
        if (Distance(projected.X, projected.Y, from.X, from.Y) <= endpointThreshold)
        {
            return FactoryMapTopologyOperationResult.Succeeded(from.Id, reusedEndpoint: true);
        }

        if (Distance(projected.X, projected.Y, to.X, to.Y) <= endpointThreshold)
        {
            return FactoryMapTopologyOperationResult.Succeeded(to.Id, reusedEndpoint: true);
        }

        var points = ClonePoints(map.ConnectionPoints);
        var inserted = new FactoryMapConnectionPoint
        {
            Id = CreateUniqueId("point", points.Select(point => point.Id)),
            Kind = FactoryMapConnectionPointKinds.Free,
            X = projected.X,
            Y = projected.Y
        };
        points.Add(inserted);
        var segments = CloneSegments(map.Segments);
        segments.RemoveAll(candidate => string.Equals(candidate.Id, segment.Id, StringComparison.OrdinalIgnoreCase));
        segments.Add(CreateSegment(segment.FromPointId, inserted.Id, segment.ZIndex));
        segments.Add(CreateSegment(inserted.Id, segment.ToPointId, segment.ZIndex));
        var result = CommitIfValid(map, points, segments, inserted.Id);
        return result;
    }

    public FactoryMapTopologyOperationResult SplitSegmentWithJunctionAt(
        FactoryMapDeviceViewData map,
        string segmentId,
        double x,
        double y,
        double gridSize,
        double endpointThreshold)
    {
        var segment = map.Segments.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, segmentId, StringComparison.OrdinalIgnoreCase));
        if (segment is null
            || !TryFindPoint(map, segment.FromPointId, out var from)
            || !TryFindPoint(map, segment.ToPointId, out var to))
        {
            return FactoryMapTopologyOperationResult.Failed("需要拆分的线段不存在或端点无效。");
        }

        var horizontal = NearlyEqual(from.Y, to.Y);
        var vertical = NearlyEqual(from.X, to.X);
        if (!horizontal && !vertical)
        {
            return FactoryMapTopologyOperationResult.Failed("只有水平或垂直线段可以创建分支连接点。");
        }

        var projected = ProjectAndSnap(from, to, x, y, gridSize);
        if (Distance(projected.X, projected.Y, from.X, from.Y) <= endpointThreshold)
        {
            return FactoryMapTopologyOperationResult.Succeeded(from.Id, reusedEndpoint: true);
        }

        if (Distance(projected.X, projected.Y, to.X, to.Y) <= endpointThreshold)
        {
            return FactoryMapTopologyOperationResult.Succeeded(to.Id, reusedEndpoint: true);
        }

        var points = ClonePoints(map.ConnectionPoints);
        var inserted = new FactoryMapConnectionPoint
        {
            Id = CreateUniqueId("junction", points.Select(point => point.Id)),
            Kind = FactoryMapConnectionPointKinds.Junction,
            JunctionAxis = horizontal
                ? FactoryMapJunctionAxes.Horizontal
                : FactoryMapJunctionAxes.Vertical,
            X = projected.X,
            Y = projected.Y
        };
        points.Add(inserted);
        var segments = CloneSegments(map.Segments);
        segments.RemoveAll(candidate => string.Equals(candidate.Id, segment.Id, StringComparison.OrdinalIgnoreCase));
        segments.Add(CreateSegment(segment.FromPointId, inserted.Id, segment.ZIndex));
        segments.Add(CreateSegment(inserted.Id, segment.ToPointId, segment.ZIndex));
        return CommitIfValid(map, points, segments, inserted.Id);
    }

    public FactoryMapTopologyOperationResult AddFreePoint(
        FactoryMapDeviceViewData map,
        double x,
        double y,
        double gridSize)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(gridSize) || gridSize <= 0)
        {
            return FactoryMapTopologyOperationResult.Failed("连接点坐标无效。");
        }

        var points = ClonePoints(map.ConnectionPoints);
        var point = new FactoryMapConnectionPoint
        {
            Id = CreateUniqueId("point", points.Select(item => item.Id)),
            Kind = FactoryMapConnectionPointKinds.Free,
            X = Snap(Math.Max(0, x), gridSize),
            Y = Snap(Math.Max(0, y), gridSize)
        };
        points.Add(point);
        return CommitIfValid(map, points, CloneSegments(map.Segments), point.Id);
    }

    public FactoryMapTopologyOperationResult DeleteConnectionPoint(
        FactoryMapDeviceViewData map,
        string pointId)
    {
        if (!TryFindPoint(map, pointId, out var point))
        {
            return FactoryMapTopologyOperationResult.Failed("连接点不存在。");
        }

        if (point.Kind == FactoryMapConnectionPointKinds.Attached)
        {
            return FactoryMapTopologyOperationResult.Failed("节点附属连接点不能删除。");
        }

        var points = ClonePoints(map.ConnectionPoints);
        points.RemoveAll(candidate => string.Equals(candidate.Id, point.Id, StringComparison.OrdinalIgnoreCase));
        var segments = CloneSegments(map.Segments);
        segments.RemoveAll(segment => ReferencesPoint(segment, point.Id));
        var result = CommitIfValid(map, points, segments);
        if (result.Success)
        {
            NormalizeJunctions(map);
            CleanupOrphanBendPoints(map);
        }

        return result;
    }

    public FactoryMapTopologyOperationResult PromoteBendToFree(
        FactoryMapDeviceViewData map,
        string pointId)
    {
        var points = ClonePoints(map.ConnectionPoints);
        var point = points.FirstOrDefault(candidate => string.Equals(candidate.Id, pointId, StringComparison.OrdinalIgnoreCase));
        if (point is null || point.Kind != FactoryMapConnectionPointKinds.Bend)
        {
            return FactoryMapTopologyOperationResult.Failed("指定对象不是折弯点。");
        }

        point.Kind = FactoryMapConnectionPointKinds.Free;
        return CommitIfValid(map, points, CloneSegments(map.Segments), point.Id);
    }

    public FactoryMapTopologyOperationResult ConvertJunctionToFree(
        FactoryMapDeviceViewData map,
        string pointId)
    {
        var points = ClonePoints(map.ConnectionPoints);
        var point = points.FirstOrDefault(candidate => string.Equals(candidate.Id, pointId, StringComparison.OrdinalIgnoreCase));
        if (point is null || point.Kind != FactoryMapConnectionPointKinds.Junction)
        {
            return FactoryMapTopologyOperationResult.Failed("指定对象不是分支连接点。");
        }

        point.Kind = FactoryMapConnectionPointKinds.Free;
        point.JunctionAxis = string.Empty;
        return CommitIfValid(map, points, CloneSegments(map.Segments), point.Id);
    }

    public int CleanupOrphanBendPoints(FactoryMapDeviceViewData map)
    {
        var removedCount = 0;
        while (true)
        {
            var degrees = BuildDegrees(map.Segments);
            var orphan = map.ConnectionPoints.FirstOrDefault(point =>
                point.Kind == FactoryMapConnectionPointKinds.Bend
                && (!degrees.TryGetValue(point.Id, out var degree) || degree <= 1));
            if (orphan is null)
            {
                return removedCount;
            }

            map.Segments.RemoveAll(segment => ReferencesPoint(segment, orphan.Id));
            map.ConnectionPoints.Remove(orphan);
            removedCount++;
        }
    }

    public int NormalizeJunctions(FactoryMapDeviceViewData map)
    {
        var points = ClonePoints(map.ConnectionPoints);
        var segments = CloneSegments(map.Segments);
        var changed = 0;
        while (true)
        {
            var junction = points.FirstOrDefault(point =>
                point.Kind == FactoryMapConnectionPointKinds.Junction
                && segments.Count(segment => ReferencesPoint(segment, point.Id)) < 3);
            if (junction is null)
            {
                break;
            }

            var incident = segments.Where(segment => ReferencesPoint(segment, junction.Id)).ToArray();
            if (incident.Length == 0)
            {
                points.Remove(junction);
                changed++;
                continue;
            }

            if (incident.Length == 1)
            {
                junction.Kind = FactoryMapConnectionPointKinds.Free;
                junction.JunctionAxis = string.Empty;
                changed++;
                continue;
            }

            var pointById = points.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
            var firstId = GetOtherPointId(incident[0], junction.Id);
            var secondId = GetOtherPointId(incident[1], junction.Id);
            if (!pointById.TryGetValue(firstId, out var first)
                || !pointById.TryGetValue(secondId, out var second))
            {
                return 0;
            }

            var collinear = (NearlyEqual(first.X, junction.X) && NearlyEqual(junction.X, second.X))
                || (NearlyEqual(first.Y, junction.Y) && NearlyEqual(junction.Y, second.Y));
            if (!collinear)
            {
                junction.Kind = FactoryMapConnectionPointKinds.Bend;
                junction.JunctionAxis = string.Empty;
                changed++;
                continue;
            }

            points.Remove(junction);
            segments.Remove(incident[0]);
            segments.Remove(incident[1]);
            if (!segments.Any(segment =>
                    (string.Equals(segment.FromPointId, first.Id, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(segment.ToPointId, second.Id, StringComparison.OrdinalIgnoreCase))
                    || (string.Equals(segment.FromPointId, second.Id, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(segment.ToPointId, first.Id, StringComparison.OrdinalIgnoreCase))))
            {
                segments.Add(new FactoryMapSegment
                {
                    Id = $"segment-{Guid.NewGuid():N}",
                    FromPointId = first.Id,
                    ToPointId = second.Id,
                    ZIndex = Math.Max(incident[0].ZIndex, incident[1].ZIndex)
                });
            }

            changed++;
        }

        if (changed == 0)
        {
            return 0;
        }

        var result = CommitIfValid(map, points, segments);
        return result.Success ? changed : 0;
    }

    public int GetDegree(FactoryMapDeviceViewData map, string pointId)
    {
        return map.Segments.Count(segment => ReferencesPoint(segment, pointId));
    }

    public FactoryMapTopologyValidationResult ValidateTopology(FactoryMapDeviceViewData map)
    {
        var errors = new List<string>();
        var pointById = new Dictionary<string, FactoryMapConnectionPoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var point in map.ConnectionPoints)
        {
            if (string.IsNullOrWhiteSpace(point.Id))
            {
                errors.Add("存在没有 ID 的连接点。");
                continue;
            }

            if (!pointById.TryAdd(point.Id, point))
            {
                errors.Add($"连接点 ID 重复：{point.Id}。");
            }

            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            {
                errors.Add($"连接点坐标无效：{point.Id}。");
            }
        }

        var segmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var endpointPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var degrees = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in map.Segments)
        {
            if (string.IsNullOrWhiteSpace(segment.Id) || !segmentIds.Add(segment.Id))
            {
                errors.Add($"线段 ID 为空或重复：{segment.Id}。");
            }

            if (!pointById.TryGetValue(segment.FromPointId, out var from)
                || !pointById.TryGetValue(segment.ToPointId, out var to))
            {
                errors.Add($"线段 {segment.Id} 引用了不存在的连接点。");
                continue;
            }

            AddDegree(degrees, from.Id);
            AddDegree(degrees, to.Id);
            if (string.Equals(from.Id, to.Id, StringComparison.OrdinalIgnoreCase) || IsSamePoint(from, to))
            {
                errors.Add($"线段 {segment.Id} 是零长度线段。");
            }
            else if (!IsOrthogonal(from, to))
            {
                errors.Add($"线段 {segment.Id} 不是正交线段。");
            }

            if (!endpointPairs.Add(CreateEndpointPairKey(from.Id, to.Id)))
            {
                errors.Add($"线段端点重复：{from.Id} 与 {to.Id}。");
            }
        }

        foreach (var point in map.ConnectionPoints.Where(point => point.Kind == FactoryMapConnectionPointKinds.Bend))
        {
            if (degrees.TryGetValue(point.Id, out var degree) && degree > 2)
            {
                errors.Add($"折弯点 {point.Id} 的连接数不能大于 2。");
            }
        }

        return new FactoryMapTopologyValidationResult(errors);
    }

    private FactoryMapTopologyOperationResult CommitIfValid(
        FactoryMapDeviceViewData map,
        List<FactoryMapConnectionPoint> points,
        List<FactoryMapSegment> segments,
        string? pointId = null)
    {
        var candidate = new FactoryMapDeviceViewData
        {
            Devices = map.Devices,
            ConnectionPoints = points,
            Segments = segments
        };
        var validation = ValidateTopology(candidate);
        if (!validation.IsValid)
        {
            return FactoryMapTopologyOperationResult.Failed(string.Join(Environment.NewLine, validation.Errors));
        }

        map.ConnectionPoints = points;
        map.Segments = segments;
        return FactoryMapTopologyOperationResult.Succeeded(pointId);
    }

    private static FactoryMapSegment CreateSegment(string fromPointId, string toPointId, int zIndex)
    {
        return new FactoryMapSegment
        {
            Id = $"segment-{Guid.NewGuid():N}",
            FromPointId = fromPointId,
            ToPointId = toPointId,
            ZIndex = zIndex
        };
    }

    private static bool HasPath(FactoryMapDeviceViewData map, string startId, string targetId)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in map.Segments)
        {
            Add(segment.FromPointId, segment.ToPointId);
            Add(segment.ToPointId, segment.FromPointId);
        }

        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startId };
        pending.Enqueue(startId);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var neighbor in neighbors)
            {
                if (string.Equals(neighbor, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (visited.Add(neighbor))
                {
                    pending.Enqueue(neighbor);
                }
            }
        }

        return false;

        void Add(string key, string value)
        {
            if (!adjacency.TryGetValue(key, out var values))
            {
                values = [];
                adjacency[key] = values;
            }

            values.Add(value);
        }
    }

    private static (double X, double Y) ProjectAndSnap(
        FactoryMapConnectionPoint from,
        FactoryMapConnectionPoint to,
        double x,
        double y,
        double gridSize)
    {
        if (NearlyEqual(from.Y, to.Y))
        {
            return (Math.Clamp(Snap(x, gridSize), Math.Min(from.X, to.X), Math.Max(from.X, to.X)), from.Y);
        }

        return (from.X, Math.Clamp(Snap(y, gridSize), Math.Min(from.Y, to.Y), Math.Max(from.Y, to.Y)));
    }

    private static bool TryFindPoint(
        FactoryMapDeviceViewData map,
        string pointId,
        out FactoryMapConnectionPoint point)
    {
        point = map.ConnectionPoints.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, pointId, StringComparison.OrdinalIgnoreCase))!;
        return point is not null;
    }

    private static bool ReferencesPoint(FactoryMapSegment segment, string pointId)
    {
        return string.Equals(segment.FromPointId, pointId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment.ToPointId, pointId, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetOtherPointId(FactoryMapSegment segment, string pointId)
    {
        return string.Equals(segment.FromPointId, pointId, StringComparison.OrdinalIgnoreCase)
            ? segment.ToPointId
            : segment.FromPointId;
    }

    private static List<FactoryMapConnectionPoint> ClonePoints(IEnumerable<FactoryMapConnectionPoint> points)
    {
        return points.Select(point => new FactoryMapConnectionPoint
        {
            Id = point.Id,
            Kind = point.Kind,
            OwnerNodeId = point.OwnerNodeId,
            Side = point.Side,
            JunctionAxis = point.JunctionAxis,
            X = point.X,
            Y = point.Y
        }).ToList();
    }

    private static List<FactoryMapSegment> CloneSegments(IEnumerable<FactoryMapSegment> segments)
    {
        return segments.Select(segment => new FactoryMapSegment
        {
            Id = segment.Id,
            FromPointId = segment.FromPointId,
            ToPointId = segment.ToPointId,
            ZIndex = segment.ZIndex
        }).ToList();
    }

    private static Dictionary<string, int> BuildDegrees(IEnumerable<FactoryMapSegment> segments)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in segments)
        {
            AddDegree(result, segment.FromPointId);
            AddDegree(result, segment.ToPointId);
        }

        return result;
    }

    private static void AddDegree(IDictionary<string, int> degrees, string pointId)
    {
        degrees[pointId] = degrees.TryGetValue(pointId, out var current) ? current + 1 : 1;
    }

    private static string CreateUniqueId(string prefix, IEnumerable<string> existingIds)
    {
        var used = existingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string id;
        do
        {
            id = $"{prefix}-{Guid.NewGuid():N}";
        }
        while (used.Contains(id));

        return id;
    }

    private static string CreateEndpointPairKey(string first, string second)
    {
        return string.Compare(first, second, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{first}\u001F{second}"
            : $"{second}\u001F{first}";
    }

    private static bool IsOrthogonal(FactoryMapConnectionPoint first, FactoryMapConnectionPoint second)
    {
        return NearlyEqual(first.X, second.X) || NearlyEqual(first.Y, second.Y);
    }

    private static bool IsSamePoint(FactoryMapConnectionPoint first, FactoryMapConnectionPoint second)
    {
        return NearlyEqual(first.X, second.X) && NearlyEqual(first.Y, second.Y);
    }

    private static bool NearlyEqual(double first, double second)
    {
        return Math.Abs(first - second) < Epsilon;
    }

    private static double Snap(double value, double gridSize)
    {
        return Math.Round(value / gridSize, MidpointRounding.AwayFromZero) * gridSize;
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
