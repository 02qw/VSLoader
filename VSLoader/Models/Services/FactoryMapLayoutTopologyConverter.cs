using System.Security.Cryptography;
using System.Text;
using System.IO;
using VSLoader.Models;
using WpfPoint = System.Windows.Point;

namespace VSLoader.Services;

internal static class FactoryMapLayoutTopologyConverter
{
    private const double Epsilon = 0.001;

    internal sealed record ConversionResult(
        List<FactoryMapConnectionPoint> Points,
        List<FactoryMapSegment> Segments,
        int InvalidSegmentCount);

    internal sealed record LegacyProjection(
        List<FactoryMapConnectorViewNode> Connectors,
        List<FactoryMapDeviceEdgeViewData> Edges);

    public static string CreateStableNodeId(string key)
    {
        return $"node-{CreateHash(key.Trim().ToUpperInvariant(), 16)}";
    }

    public static string CreateAttachedPointId(string nodeId, string side)
    {
        return $"{nodeId}:{FactoryMapEndpointGeometryService.NormalizePort(side)}";
    }

    public static ConversionResult BuildFromVersion4(
        FactoryMapLayoutConfig config,
        IReadOnlyList<FactoryMapDeviceViewNode> devices,
        bool allowAttachedGeometryMismatch = false)
    {
        var points = CreateAttachedPoints(devices);
        var pointIds = points.Select(point => point.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var source in config.ConnectionPoints ?? [])
        {
            var kind = FactoryMapConnectionPointKinds.Normalize(source.Kind);
            var pointId = source.Id?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(pointId) && pointIds.Contains(pointId))
            {
                throw new InvalidDataException($"连接点 ID 与节点附属点冲突或重复：{pointId}。");
            }

            if (kind == FactoryMapConnectionPointKinds.Attached
                || string.IsNullOrWhiteSpace(pointId)
                || !double.IsFinite(source.X)
                || !double.IsFinite(source.Y)
                || !pointIds.Add(pointId))
            {
                continue;
            }

            points.Add(new FactoryMapConnectionPoint
            {
                Id = pointId,
                Kind = kind,
                OwnerNodeId = string.Empty,
                Side = string.Empty,
                JunctionAxis = kind == FactoryMapConnectionPointKinds.Junction
                    ? FactoryMapJunctionAxes.Normalize(source.JunctionAxis)
                    : string.Empty,
                X = Math.Max(0, source.X),
                Y = Math.Max(0, source.Y)
            });
        }

        var pointById = points.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
        var segments = new List<FactoryMapSegment>();
        var segmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var endpointPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var invalidCount = 0;
        foreach (var source in config.Segments ?? [])
        {
            var fromId = source.FromPointId?.Trim() ?? string.Empty;
            var toId = source.ToPointId?.Trim() ?? string.Empty;
            var id = source.Id?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)
                || string.IsNullOrWhiteSpace(fromId)
                || string.IsNullOrWhiteSpace(toId)
                || string.Equals(fromId, toId, StringComparison.OrdinalIgnoreCase)
                || !segmentIds.Add(id)
                || !pointById.TryGetValue(fromId, out var from)
                || !pointById.TryGetValue(toId, out var to)
                || IsSamePoint(from, to)
                || (!IsOrthogonal(from, to)
                    && !(allowAttachedGeometryMismatch
                        && (from.Kind == FactoryMapConnectionPointKinds.Attached
                            || to.Kind == FactoryMapConnectionPointKinds.Attached)))
                || !endpointPairs.Add(CreateEndpointPairKey(fromId, toId)))
            {
                invalidCount++;
                continue;
            }

            segments.Add(new FactoryMapSegment
            {
                Id = id,
                FromPointId = from.Id,
                ToPointId = to.Id,
                ZIndex = source.ZIndex
            });
        }

        if (config.Version == 4)
        {
            MigrateVersion4FreePoints(points, segments);
        }

        return new ConversionResult(points, segments, invalidCount);
    }

    private static void MigrateVersion4FreePoints(
        IReadOnlyCollection<FactoryMapConnectionPoint> points,
        IReadOnlyCollection<FactoryMapSegment> segments)
    {
        var pointById = points.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var point in points.Where(point => point.Kind == FactoryMapConnectionPointKinds.Free))
        {
            var neighbors = segments
                .Where(segment => string.Equals(segment.FromPointId, point.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segment.ToPointId, point.Id, StringComparison.OrdinalIgnoreCase))
                .Select(segment => string.Equals(segment.FromPointId, point.Id, StringComparison.OrdinalIgnoreCase)
                    ? segment.ToPointId
                    : segment.FromPointId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => pointById.GetValueOrDefault(id))
                .Where(neighbor => neighbor is not null)
                .Cast<FactoryMapConnectionPoint>()
                .ToArray();
            if (neighbors.Length < 2)
            {
                continue;
            }

            var hasLeft = neighbors.Any(neighbor => NearlyEqual(neighbor.Y, point.Y) && neighbor.X < point.X);
            var hasRight = neighbors.Any(neighbor => NearlyEqual(neighbor.Y, point.Y) && neighbor.X > point.X);
            var hasTop = neighbors.Any(neighbor => NearlyEqual(neighbor.X, point.X) && neighbor.Y < point.Y);
            var hasBottom = neighbors.Any(neighbor => NearlyEqual(neighbor.X, point.X) && neighbor.Y > point.Y);
            var horizontal = hasLeft && hasRight;
            var vertical = hasTop && hasBottom;
            if (!horizontal && !vertical)
            {
                continue;
            }

            point.Kind = FactoryMapConnectionPointKinds.Junction;
            point.JunctionAxis = horizontal && vertical
                ? FactoryMapJunctionAxes.Locked
                : horizontal
                    ? FactoryMapJunctionAxes.Horizontal
                    : FactoryMapJunctionAxes.Vertical;
        }
    }

    public static ConversionResult BuildFromLegacy(
        IReadOnlyList<FactoryMapDeviceViewNode> devices,
        IReadOnlyList<FactoryMapConnectorViewNode> connectors,
        IReadOnlyList<FactoryMapDeviceEdgeViewData> edges)
    {
        var points = CreateAttachedPoints(devices);
        points.AddRange(connectors
            .Where(connector => !string.IsNullOrWhiteSpace(connector.Id)
                && double.IsFinite(connector.X)
                && double.IsFinite(connector.Y))
            .Select(connector => new FactoryMapConnectionPoint
            {
                Id = connector.Id.Trim(),
                Kind = FactoryMapConnectionPointKinds.Free,
                X = Math.Max(0, connector.X),
                Y = Math.Max(0, connector.Y)
            }));

        points = points
            .GroupBy(point => point.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var pointById = points.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
        var segments = new List<FactoryMapSegment>();
        var endpointPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var invalidCount = 0;

        for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
        {
            var edge = edges[edgeIndex];
            if (!TryGetLegacyEndpointPointId(edge.From, edge.FromPort, out var fromPointId)
                || !TryGetLegacyEndpointPointId(edge.To, edge.ToPort, out var toPointId)
                || !pointById.TryGetValue(fromPointId, out var fromPoint)
                || !pointById.TryGetValue(toPointId, out var toPoint))
            {
                invalidCount++;
                continue;
            }

            var middlePoints = FactoryMapOrthogonalPathService.Normalize(
                new WpfPoint(fromPoint.X, fromPoint.Y),
                edge.Points,
                new WpfPoint(toPoint.X, toPoint.Y));
            var chain = new List<FactoryMapConnectionPoint> { fromPoint };
            for (var pointIndex = 0; pointIndex < middlePoints.Count; pointIndex++)
            {
                var middle = middlePoints[pointIndex];
                var bendId = $"bend-{CreateHash($"{edgeIndex}|{pointIndex}|{middle.X:R}|{middle.Y:R}|{fromPointId}|{toPointId}", 20)}";
                var bend = new FactoryMapConnectionPoint
                {
                    Id = bendId,
                    Kind = FactoryMapConnectionPointKinds.Bend,
                    X = Math.Max(0, middle.X),
                    Y = Math.Max(0, middle.Y)
                };
                points.Add(bend);
                pointById[bend.Id] = bend;
                chain.Add(bend);
            }

            chain.Add(toPoint);
            for (var segmentIndex = 0; segmentIndex < chain.Count - 1; segmentIndex++)
            {
                var start = chain[segmentIndex];
                var end = chain[segmentIndex + 1];
                if (IsSamePoint(start, end) || !IsOrthogonal(start, end))
                {
                    invalidCount++;
                    continue;
                }

                if (!endpointPairs.Add(CreateEndpointPairKey(start.Id, end.Id)))
                {
                    invalidCount++;
                    continue;
                }

                segments.Add(new FactoryMapSegment
                {
                    Id = $"segment-{CreateHash($"{edgeIndex}|{segmentIndex}|{start.Id}|{end.Id}", 20)}",
                    FromPointId = start.Id,
                    ToPointId = end.Id,
                    ZIndex = edgeIndex
                });
            }
        }

        return new ConversionResult(points, segments, invalidCount);
    }

    public static LegacyProjection BuildLegacyProjection(
        IReadOnlyList<FactoryMapDeviceViewNode> devices,
        IReadOnlyList<FactoryMapConnectionPoint> points,
        IReadOnlyList<FactoryMapSegment> segments)
    {
        var pointById = points.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
        var deviceById = devices.ToDictionary(device => device.Id, StringComparer.OrdinalIgnoreCase);
        var connectors = points
            .Where(point => point.Kind == FactoryMapConnectionPointKinds.Free)
            .Select(point => new FactoryMapConnectorViewNode { Id = point.Id, X = point.X, Y = point.Y })
            .ToList();
        var connectorById = connectors.ToDictionary(connector => connector.Id, StringComparer.OrdinalIgnoreCase);
        var adjacency = BuildAdjacency(segments);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<FactoryMapDeviceEdgeViewData>();

        foreach (var startPoint in points.Where(point => point.Kind != FactoryMapConnectionPointKinds.Bend))
        {
            if (!adjacency.TryGetValue(startPoint.Id, out var adjacentSegments))
            {
                continue;
            }

            foreach (var firstSegment in adjacentSegments)
            {
                if (visited.Contains(firstSegment.Id))
                {
                    continue;
                }

                var chainPoints = new List<FactoryMapConnectionPoint> { startPoint };
                var currentPoint = startPoint;
                var currentSegment = firstSegment;
                while (true)
                {
                    visited.Add(currentSegment.Id);
                    var nextPointId = string.Equals(currentSegment.FromPointId, currentPoint.Id, StringComparison.OrdinalIgnoreCase)
                        ? currentSegment.ToPointId
                        : currentSegment.FromPointId;
                    if (!pointById.TryGetValue(nextPointId, out var nextPoint))
                    {
                        break;
                    }

                    chainPoints.Add(nextPoint);
                    if (nextPoint.Kind != FactoryMapConnectionPointKinds.Bend)
                    {
                        break;
                    }

                    if (!adjacency.TryGetValue(nextPoint.Id, out var nextSegments))
                    {
                        break;
                    }

                    var continuation = nextSegments.FirstOrDefault(segment => !visited.Contains(segment.Id));
                    if (continuation is null)
                    {
                        break;
                    }

                    currentPoint = nextPoint;
                    currentSegment = continuation;
                }

                if (chainPoints.Count < 2
                    || chainPoints[^1].Kind == FactoryMapConnectionPointKinds.Bend
                    || !TryCreateLegacyEndpoint(chainPoints[0], deviceById, connectorById, out var from, out var fromPort)
                    || !TryCreateLegacyEndpoint(chainPoints[^1], deviceById, connectorById, out var to, out var toPort))
                {
                    continue;
                }

                edges.Add(new FactoryMapDeviceEdgeViewData
                {
                    From = from,
                    FromPort = fromPort,
                    To = to,
                    ToPort = toPort,
                    Points = chainPoints
                        .Skip(1)
                        .Take(chainPoints.Count - 2)
                        .Select(point => new FactoryMapPoint { X = point.X, Y = point.Y })
                        .ToList()
                });
            }
        }

        return new LegacyProjection(connectors, edges);
    }

    internal static List<FactoryMapConnectionPoint> CreateAttachedPoints(IReadOnlyList<FactoryMapDeviceViewNode> devices)
    {
        var result = new List<FactoryMapConnectionPoint>(devices.Count * 4);
        foreach (var device in devices)
        {
            foreach (var side in FactoryMapPortKinds.All)
            {
                var position = FactoryMapEndpointGeometryService.GetPortPoint(
                    FactoryMapEndpointViewData.FromDevice(device),
                    side);
                result.Add(new FactoryMapConnectionPoint
                {
                    Id = CreateAttachedPointId(device.Id, side),
                    Kind = FactoryMapConnectionPointKinds.Attached,
                    OwnerNodeId = device.Id,
                    Side = side,
                    X = position.X,
                    Y = position.Y
                });
            }
        }

        return result;
    }

    private static bool TryGetLegacyEndpointPointId(
        FactoryMapEndpointViewData endpoint,
        string port,
        out string pointId)
    {
        if (endpoint.Device is not null && !string.IsNullOrWhiteSpace(endpoint.Device.Id))
        {
            pointId = CreateAttachedPointId(endpoint.Device.Id, port);
            return true;
        }

        if (endpoint.Connector is not null && !string.IsNullOrWhiteSpace(endpoint.Connector.Id))
        {
            pointId = endpoint.Connector.Id.Trim();
            return true;
        }

        pointId = string.Empty;
        return false;
    }

    private static bool TryCreateLegacyEndpoint(
        FactoryMapConnectionPoint point,
        IReadOnlyDictionary<string, FactoryMapDeviceViewNode> deviceById,
        IReadOnlyDictionary<string, FactoryMapConnectorViewNode> connectorById,
        out FactoryMapEndpointViewData endpoint,
        out string port)
    {
        if (point.Kind == FactoryMapConnectionPointKinds.Attached
            && deviceById.TryGetValue(point.OwnerNodeId, out var device))
        {
            endpoint = FactoryMapEndpointViewData.FromDevice(device);
            port = FactoryMapEndpointGeometryService.NormalizePort(point.Side);
            return true;
        }

        if (point.Kind == FactoryMapConnectionPointKinds.Free
            && connectorById.TryGetValue(point.Id, out var connector))
        {
            endpoint = FactoryMapEndpointViewData.FromConnector(connector);
            port = FactoryMapPortKinds.Right;
            return true;
        }

        endpoint = new FactoryMapEndpointViewData();
        port = FactoryMapPortKinds.Right;
        return false;
    }

    private static Dictionary<string, List<FactoryMapSegment>> BuildAdjacency(IReadOnlyList<FactoryMapSegment> segments)
    {
        var result = new Dictionary<string, List<FactoryMapSegment>>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in segments)
        {
            Add(segment.FromPointId, segment);
            Add(segment.ToPointId, segment);
        }

        return result;

        void Add(string pointId, FactoryMapSegment segment)
        {
            if (!result.TryGetValue(pointId, out var values))
            {
                values = [];
                result[pointId] = values;
            }

            values.Add(segment);
        }
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

    private static string CreateEndpointPairKey(string first, string second)
    {
        return string.Compare(first, second, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{first}\u001F{second}"
            : $"{second}\u001F{first}";
    }

    private static string CreateHash(string value, int length)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..length];
    }
}
