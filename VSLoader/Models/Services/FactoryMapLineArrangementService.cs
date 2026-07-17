using VSLoader.Models;

namespace VSLoader.Services;

public sealed class FactoryMapLineArrangementService
{
    private readonly FactoryMapLogicalRouteService logicalRouteService = new();
    private readonly FactoryMapOrthogonalRouterService routerService = new();
    private readonly FactoryMapTopologyService topologyService = new();

    public FactoryMapLineArrangementResult ArrangeAll(FactoryMapDeviceViewData map, double gridSize)
    {
        return Arrange(map, null, gridSize);
    }

    public FactoryMapLineArrangementResult ArrangeConnectedTo(
        FactoryMapDeviceViewData map,
        IReadOnlyCollection<string> anchorPointIds,
        double gridSize)
    {
        var filter = anchorPointIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Arrange(map, filter, gridSize);
    }

    private FactoryMapLineArrangementResult Arrange(
        FactoryMapDeviceViewData map,
        HashSet<string>? anchorFilter,
        double gridSize)
    {
        if (!double.IsFinite(gridSize) || gridSize <= 0)
        {
            return FactoryMapLineArrangementResult.Failed("线路整理网格参数无效。");
        }

        if (map.Segments.Count == 0)
        {
            return FactoryMapLineArrangementResult.Succeeded(0, 0, 0);
        }

        var candidate = CloneMap(map);
        var enumeration = logicalRouteService.Enumerate(candidate);
        if (!enumeration.Success)
        {
            return FactoryMapLineArrangementResult.Failed(enumeration.ErrorMessage ?? "逻辑线路枚举失败。");
        }

        var routes = enumeration.Routes
            .Where(route => anchorFilter is null
                || anchorFilter.Contains(route.StartPointId)
                || anchorFilter.Contains(route.EndPointId))
            .ToArray();
        if (routes.Length == 0)
        {
            return FactoryMapLineArrangementResult.Succeeded(0, 0, 0);
        }

        var removedBendCount = 0;
        var createdBendCount = 0;
        var pointIds = candidate.ConnectionPoints.Select(point => point.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var routeIndex = 0; routeIndex < routes.Length; routeIndex++)
        {
            var route = routes[routeIndex];
            var from = candidate.ConnectionPoints.FirstOrDefault(point =>
                string.Equals(point.Id, route.StartPointId, StringComparison.OrdinalIgnoreCase));
            var to = candidate.ConnectionPoints.FirstOrDefault(point =>
                string.Equals(point.Id, route.EndPointId, StringComparison.OrdinalIgnoreCase));
            if (from is null || to is null)
            {
                return FactoryMapLineArrangementResult.Failed($"第 {routeIndex + 1} 条逻辑线路的锚点不存在。");
            }

            var routed = routerService.Route(candidate, from, to, gridSize);
            if (!routed.Success)
            {
                return FactoryMapLineArrangementResult.Failed(
                    $"第 {routeIndex + 1} 条逻辑线路无法路由：{routed.ErrorMessage}");
            }

            candidate.Segments.RemoveAll(segment => route.SegmentIds.Contains(segment.Id, StringComparer.OrdinalIgnoreCase));
            removedBendCount += candidate.ConnectionPoints.RemoveAll(point =>
                route.BendPointIds.Contains(point.Id, StringComparer.OrdinalIgnoreCase));
            foreach (var removedId in route.BendPointIds)
            {
                pointIds.Remove(removedId);
            }

            var pathIds = new List<string> { from.Id };
            foreach (var routePoint in routed.Points.Skip(1).Take(Math.Max(0, routed.Points.Count - 2)))
            {
                var bendId = CreateUniqueId("bend", pointIds);
                pointIds.Add(bendId);
                candidate.ConnectionPoints.Add(new FactoryMapConnectionPoint
                {
                    Id = bendId,
                    Kind = FactoryMapConnectionPointKinds.Bend,
                    X = routePoint.X,
                    Y = routePoint.Y
                });
                pathIds.Add(bendId);
                createdBendCount++;
            }

            pathIds.Add(to.Id);
            for (var index = 0; index < pathIds.Count - 1; index++)
            {
                candidate.Segments.Add(new FactoryMapSegment
                {
                    Id = $"segment-{Guid.NewGuid():N}",
                    FromPointId = pathIds[index],
                    ToPointId = pathIds[index + 1],
                    ZIndex = route.BaseZIndex + index
                });
            }
        }

        var validation = topologyService.ValidateTopology(candidate);
        if (!validation.IsValid)
        {
            return FactoryMapLineArrangementResult.Failed(
                $"线路整理后的拓扑校验失败：{string.Join(Environment.NewLine, validation.Errors)}");
        }

        map.ConnectionPoints = candidate.ConnectionPoints;
        map.Segments = candidate.Segments;
        return FactoryMapLineArrangementResult.Succeeded(routes.Length, removedBendCount, createdBendCount);
    }

    private static FactoryMapDeviceViewData CloneMap(FactoryMapDeviceViewData map)
    {
        return new FactoryMapDeviceViewData
        {
            TopologyAuthoritative = map.TopologyAuthoritative,
            Canvas = map.Canvas,
            Devices = map.Devices.Select(device => new FactoryMapDeviceViewNode
            {
                Id = device.Id,
                Key = device.Key,
                Name = device.Name,
                X = device.X,
                Y = device.Y,
                Width = device.Width,
                Height = device.Height,
                Shortcut = device.Shortcut
            }).ToList(),
            ConnectionPoints = map.ConnectionPoints.Select(point => new FactoryMapConnectionPoint
            {
                Id = point.Id,
                Kind = point.Kind,
                OwnerNodeId = point.OwnerNodeId,
                Side = point.Side,
                JunctionAxis = point.JunctionAxis,
                X = point.X,
                Y = point.Y
            }).ToList(),
            Segments = map.Segments.Select(segment => new FactoryMapSegment
            {
                Id = segment.Id,
                FromPointId = segment.FromPointId,
                ToPointId = segment.ToPointId,
                ZIndex = segment.ZIndex
            }).ToList()
        };
    }

    private static string CreateUniqueId(string prefix, IReadOnlySet<string> usedIds)
    {
        string id;
        do
        {
            id = $"{prefix}-{Guid.NewGuid():N}";
        }
        while (usedIds.Contains(id));

        return id;
    }
}
