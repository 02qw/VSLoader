using VSLoader.Models;

namespace VSLoader.Services;

internal sealed record FactoryMapLogicalRoute(
    string StartPointId,
    string EndPointId,
    IReadOnlyList<string> BendPointIds,
    IReadOnlyList<string> SegmentIds,
    int BaseZIndex);

internal sealed record FactoryMapLogicalRouteEnumerationResult(
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<FactoryMapLogicalRoute> Routes)
{
    public static FactoryMapLogicalRouteEnumerationResult Succeeded(IReadOnlyList<FactoryMapLogicalRoute> routes) =>
        new(true, null, routes);

    public static FactoryMapLogicalRouteEnumerationResult Failed(string errorMessage) =>
        new(false, errorMessage, []);
}

internal sealed class FactoryMapLogicalRouteService
{
    public FactoryMapLogicalRouteEnumerationResult Enumerate(FactoryMapDeviceViewData map)
    {
        var pointById = new Dictionary<string, FactoryMapConnectionPoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var point in map.ConnectionPoints)
        {
            if (string.IsNullOrWhiteSpace(point.Id) || !pointById.TryAdd(point.Id, point))
            {
                return FactoryMapLogicalRouteEnumerationResult.Failed($"连接点 ID 为空或重复：{point.Id}。");
            }
        }

        var segmentById = new Dictionary<string, FactoryMapSegment>(StringComparer.OrdinalIgnoreCase);
        var adjacency = new Dictionary<string, List<FactoryMapSegment>>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in map.Segments)
        {
            if (string.IsNullOrWhiteSpace(segment.Id) || !segmentById.TryAdd(segment.Id, segment))
            {
                return FactoryMapLogicalRouteEnumerationResult.Failed($"线段 ID 为空或重复：{segment.Id}。");
            }

            if (!pointById.ContainsKey(segment.FromPointId) || !pointById.ContainsKey(segment.ToPointId))
            {
                return FactoryMapLogicalRouteEnumerationResult.Failed($"线段 {segment.Id} 引用了不存在的连接点。");
            }

            AddAdjacency(segment.FromPointId, segment);
            AddAdjacency(segment.ToPointId, segment);
        }

        foreach (var bend in map.ConnectionPoints.Where(point => point.Kind == FactoryMapConnectionPointKinds.Bend))
        {
            var degree = adjacency.TryGetValue(bend.Id, out var incident) ? incident.Count : 0;
            if (degree != 2)
            {
                return FactoryMapLogicalRouteEnumerationResult.Failed($"折弯点 {bend.Id} 的连接数必须等于 2，当前为 {degree}。");
            }
        }

        var routes = new List<FactoryMapLogicalRoute>();
        var visitedSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var anchor in map.ConnectionPoints
                     .Where(point => point.Kind != FactoryMapConnectionPointKinds.Bend)
                     .OrderBy(point => point.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (!adjacency.TryGetValue(anchor.Id, out var incidentSegments))
            {
                continue;
            }

            foreach (var initialSegment in incidentSegments.OrderBy(segment => segment.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (visitedSegments.Contains(initialSegment.Id))
                {
                    continue;
                }

                var segmentIds = new List<string>();
                var bendIds = new List<string>();
                var currentPointId = anchor.Id;
                var currentSegment = initialSegment;
                while (true)
                {
                    if (!visitedSegments.Add(currentSegment.Id))
                    {
                        return FactoryMapLogicalRouteEnumerationResult.Failed("逻辑线路中存在重复线段或闭环。");
                    }

                    segmentIds.Add(currentSegment.Id);
                    var otherPointId = GetOtherPointId(currentSegment, currentPointId);
                    var otherPoint = pointById[otherPointId];
                    if (otherPoint.Kind != FactoryMapConnectionPointKinds.Bend)
                    {
                        routes.Add(new FactoryMapLogicalRoute(
                            anchor.Id,
                            otherPoint.Id,
                            bendIds.ToArray(),
                            segmentIds.ToArray(),
                            segmentIds.Max(id => segmentById[id].ZIndex)));
                        break;
                    }

                    bendIds.Add(otherPoint.Id);
                    var nextSegments = adjacency[otherPoint.Id]
                        .Where(segment => !string.Equals(segment.Id, currentSegment.Id, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    if (nextSegments.Length != 1)
                    {
                        return FactoryMapLogicalRouteEnumerationResult.Failed($"折弯点 {otherPoint.Id} 无法确定唯一后续线段。");
                    }

                    currentPointId = otherPoint.Id;
                    currentSegment = nextSegments[0];
                }
            }
        }

        if (visitedSegments.Count != map.Segments.Count)
        {
            return FactoryMapLogicalRouteEnumerationResult.Failed("地图中存在只有折弯点组成的闭环线路。");
        }

        return FactoryMapLogicalRouteEnumerationResult.Succeeded(routes);

        void AddAdjacency(string pointId, FactoryMapSegment segment)
        {
            if (!adjacency.TryGetValue(pointId, out var values))
            {
                values = [];
                adjacency[pointId] = values;
            }

            values.Add(segment);
        }
    }

    private static string GetOtherPointId(FactoryMapSegment segment, string pointId)
    {
        return string.Equals(segment.FromPointId, pointId, StringComparison.OrdinalIgnoreCase)
            ? segment.ToPointId
            : segment.FromPointId;
    }
}
