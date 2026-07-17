using VSLoader.Models;

namespace VSLoader.Services;

public sealed class FactoryMapMovementService
{
    private readonly FactoryMapTopologyService topologyService = new();
    private readonly FactoryMapLineArrangementService lineArrangementService = new();

    public FactoryMapMovementResult MoveObject(
        FactoryMapDeviceViewData map,
        FactoryMapObjectRef objectRef,
        double deltaX,
        double deltaY,
        bool snapToGrid,
        double gridSize)
    {
        if (!double.IsFinite(deltaX)
            || !double.IsFinite(deltaY)
            || !double.IsFinite(gridSize)
            || gridSize <= 0)
        {
            return FactoryMapMovementResult.Failed("移动参数无效。");
        }

        return objectRef.Kind switch
        {
            FactoryMapObjectKind.Device => MoveDevice(map, objectRef.Id, deltaX, deltaY, snapToGrid, gridSize),
            FactoryMapObjectKind.ConnectionPoint => MovePoint(map, objectRef.Id, deltaX, deltaY, snapToGrid, gridSize),
            FactoryMapObjectKind.Segment => MoveSegment(map, objectRef.Id, deltaX, deltaY, snapToGrid, gridSize),
            _ => FactoryMapMovementResult.Failed("不支持的地图对象类型。")
        };
    }

    public FactoryMapMovementResult MoveObjects(
        FactoryMapDeviceViewData map,
        IReadOnlyCollection<FactoryMapObjectRef> objectRefs,
        double deltaX,
        double deltaY,
        bool snapToGrid,
        double gridSize)
    {
        if (objectRefs is null
            || !double.IsFinite(deltaX)
            || !double.IsFinite(deltaY)
            || !double.IsFinite(gridSize)
            || gridSize <= 0)
        {
            return FactoryMapMovementResult.Failed("移动参数无效。");
        }

        if (objectRefs.Count == 0)
        {
            return FactoryMapMovementResult.Succeeded();
        }

        var devices = CloneDevices(map.Devices);
        var points = ClonePoints(map.ConnectionPoints);
        var segments = CloneSegments(map.Segments);
        var deviceById = devices.ToDictionary(device => device.Id, StringComparer.OrdinalIgnoreCase);
        var pointById = points.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
        var uniqueObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var affectedAnchorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var objectRef in objectRefs)
        {
            if (!uniqueObjects.Add($"{(int)objectRef.Kind}:{objectRef.Id}"))
            {
                continue;
            }

            if (objectRef.Kind == FactoryMapObjectKind.Device)
            {
                if (!deviceById.TryGetValue(objectRef.Id, out var device))
                {
                    return FactoryMapMovementResult.Failed("需要移动的节点不存在。");
                }

                device.X = ApplyTarget(device.X + deltaX, snapToGrid, gridSize);
                device.Y = ApplyTarget(device.Y + deltaY, snapToGrid, gridSize);
                UpdateAttachedPoints(points, device, device.X, device.Y);
                foreach (var attachedId in points
                             .Where(point => point.Kind == FactoryMapConnectionPointKinds.Attached
                                 && string.Equals(point.OwnerNodeId, device.Id, StringComparison.OrdinalIgnoreCase))
                             .Select(point => point.Id))
                {
                    affectedAnchorIds.Add(attachedId);
                }
                continue;
            }

            if (objectRef.Kind != FactoryMapObjectKind.ConnectionPoint
                || !pointById.TryGetValue(objectRef.Id, out var point))
            {
                return FactoryMapMovementResult.Failed("多选集合包含不存在或不支持移动的对象。");
            }

            if (point.Kind != FactoryMapConnectionPointKinds.Free)
            {
                return FactoryMapMovementResult.Failed("多选移动只支持节点和普通连接点。");
            }

            point.X = ApplyTarget(point.X + deltaX, snapToGrid, gridSize);
            point.Y = ApplyTarget(point.Y + deltaY, snapToGrid, gridSize);
            affectedAnchorIds.Add(point.Id);
        }

        var candidate = CreateCandidate(devices, points, segments);
        var arranged = lineArrangementService.ArrangeConnectedTo(candidate, affectedAnchorIds, gridSize);
        if (!arranged.Success)
        {
            return FactoryMapMovementResult.Failed(arranged.ErrorMessage ?? "多选对象相邻线路重排失败。");
        }

        map.Devices = devices;
        map.ConnectionPoints = candidate.ConnectionPoints;
        map.Segments = candidate.Segments;
        return FactoryMapMovementResult.Succeeded();
    }

    public static double GetKeyboardStep(bool shift, bool control, bool requireGridAlignment = false)
    {
        if (control)
        {
            return 50;
        }

        return shift && !requireGridAlignment ? 1 : FactoryMapNodeGeometryService.GridSize;
    }

    public static bool ShouldSnapKeyboardMovement(bool shift, bool requireGridAlignment) =>
        requireGridAlignment || !shift;

    private FactoryMapMovementResult MoveDevice(
        FactoryMapDeviceViewData map,
        string deviceId,
        double deltaX,
        double deltaY,
        bool snapToGrid,
        double gridSize)
    {
        var device = map.Devices.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        if (device is null)
        {
            return FactoryMapMovementResult.Failed("需要移动的节点不存在。");
        }

        var devices = CloneDevices(map.Devices);
        var movedDevice = devices.Single(candidate =>
            string.Equals(candidate.Id, device.Id, StringComparison.OrdinalIgnoreCase));
        var targetX = ApplyTarget(movedDevice.X + deltaX, snapToGrid, gridSize);
        var targetY = ApplyTarget(movedDevice.Y + deltaY, snapToGrid, gridSize);
        movedDevice.X = targetX;
        movedDevice.Y = targetY;
        var points = ClonePoints(map.ConnectionPoints);
        UpdateAttachedPoints(points, device, targetX, targetY);
        var segments = CloneSegments(map.Segments);
        var candidate = CreateCandidate(devices, points, segments);
        var attachedIds = points
            .Where(point => point.Kind == FactoryMapConnectionPointKinds.Attached
                && string.Equals(point.OwnerNodeId, device.Id, StringComparison.OrdinalIgnoreCase))
            .Select(point => point.Id)
            .ToArray();
        var arranged = lineArrangementService.ArrangeConnectedTo(candidate, attachedIds, gridSize);
        if (!arranged.Success)
        {
            return FactoryMapMovementResult.Failed(arranged.ErrorMessage ?? "节点相邻线路重排失败。");
        }

        map.Devices = devices;
        map.ConnectionPoints = candidate.ConnectionPoints;
        map.Segments = candidate.Segments;
        return FactoryMapMovementResult.Succeeded();
    }

    private FactoryMapMovementResult MovePoint(
        FactoryMapDeviceViewData map,
        string pointId,
        double deltaX,
        double deltaY,
        bool snapToGrid,
        double gridSize)
    {
        var source = map.ConnectionPoints.FirstOrDefault(point =>
            string.Equals(point.Id, pointId, StringComparison.OrdinalIgnoreCase));
        if (source is null)
        {
            return FactoryMapMovementResult.Failed("需要移动的连接点不存在。");
        }

        if (source.Kind == FactoryMapConnectionPointKinds.Attached)
        {
            return MoveDevice(map, source.OwnerNodeId, deltaX, deltaY, snapToGrid, gridSize);
        }

        if (source.Kind == FactoryMapConnectionPointKinds.Junction)
        {
            var axis = FactoryMapJunctionAxes.Normalize(source.JunctionAxis);
            if (axis == FactoryMapJunctionAxes.Locked)
            {
                return FactoryMapMovementResult.Failed("该分支连接点的主干方向不唯一，请移动相邻线段调整布局。");
            }

            if (axis == FactoryMapJunctionAxes.Horizontal)
            {
                deltaY = 0;
            }
            else
            {
                deltaX = 0;
            }
        }

        var points = ClonePoints(map.ConnectionPoints);
        var point = points.Single(candidate => string.Equals(candidate.Id, source.Id, StringComparison.OrdinalIgnoreCase));
        point.X = Math.Abs(deltaX) < 0.001
            ? point.X
            : ApplyTarget(point.X + deltaX, snapToGrid, gridSize);
        point.Y = Math.Abs(deltaY) < 0.001
            ? point.Y
            : ApplyTarget(point.Y + deltaY, snapToGrid, gridSize);
        var segments = CloneSegments(map.Segments);
        if (source.Kind is FactoryMapConnectionPointKinds.Free or FactoryMapConnectionPointKinds.Junction)
        {
            var candidate = CreateCandidate(map, points, segments);
            var arranged = lineArrangementService.ArrangeConnectedTo(candidate, [source.Id], gridSize);
            if (!arranged.Success)
            {
                return FactoryMapMovementResult.Failed(arranged.ErrorMessage ?? "连接点相邻线路重排失败。");
            }

            map.ConnectionPoints = candidate.ConnectionPoints;
            map.Segments = candidate.Segments;
            return FactoryMapMovementResult.Succeeded();
        }

        RepairOrthogonality(points, segments);
        return Commit(map, points, segments);
    }

    private FactoryMapMovementResult MoveSegment(
        FactoryMapDeviceViewData map,
        string segmentId,
        double deltaX,
        double deltaY,
        bool snapToGrid,
        double gridSize)
    {
        var segment = map.Segments.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, segmentId, StringComparison.OrdinalIgnoreCase));
        if (segment is null)
        {
            return FactoryMapMovementResult.Failed("需要移动的线段不存在。");
        }

        var points = ClonePoints(map.ConnectionPoints);
        var from = points.FirstOrDefault(point => string.Equals(point.Id, segment.FromPointId, StringComparison.OrdinalIgnoreCase));
        var to = points.FirstOrDefault(point => string.Equals(point.Id, segment.ToPointId, StringComparison.OrdinalIgnoreCase));
        if (from is null || to is null)
        {
            return FactoryMapMovementResult.Failed("线段端点不存在。");
        }

        if (NearlyEqual(from.Y, to.Y) && Math.Abs(deltaY) >= 0.001)
        {
            return MoveCollinearChannel(
                map,
                segmentId,
                horizontal: true,
                deltaY,
                snapToGrid,
                gridSize);
        }

        if (NearlyEqual(from.Y, to.Y) && Math.Abs(deltaX) >= 0.001)
        {
            return FactoryMapMovementResult.Failed("水平线段只支持上下移动。");
        }

        if (NearlyEqual(from.X, to.X) && Math.Abs(deltaX) >= 0.001)
        {
            return MoveCollinearChannel(
                map,
                segmentId,
                horizontal: false,
                deltaX,
                snapToGrid,
                gridSize);
        }

        if (NearlyEqual(from.X, to.X) && Math.Abs(deltaY) >= 0.001)
        {
            return FactoryMapMovementResult.Failed("垂直线段只支持左右移动。");
        }

        if (from.Kind != FactoryMapConnectionPointKinds.Bend
            || to.Kind != FactoryMapConnectionPointKinds.Bend)
        {
            return FactoryMapMovementResult.Failed("线段两端是固定连接点，请移动端点或添加绕行段。");
        }

        if (NearlyEqual(from.Y, to.Y))
        {
            if (Math.Abs(deltaY) >= 0.001)
            {
                var targetY = ApplyTarget(from.Y + deltaY, snapToGrid, gridSize);
                from.Y = targetY;
                to.Y = targetY;
            }
            else
            {
                from.X = ApplyTarget(from.X + deltaX, snapToGrid, gridSize);
                to.X = ApplyTarget(to.X + deltaX, snapToGrid, gridSize);
            }
        }
        else if (NearlyEqual(from.X, to.X))
        {
            if (Math.Abs(deltaX) >= 0.001)
            {
                var targetX = ApplyTarget(from.X + deltaX, snapToGrid, gridSize);
                from.X = targetX;
                to.X = targetX;
            }
            else
            {
                from.Y = ApplyTarget(from.Y + deltaY, snapToGrid, gridSize);
                to.Y = ApplyTarget(to.Y + deltaY, snapToGrid, gridSize);
            }
        }
        else
        {
            return FactoryMapMovementResult.Failed("只有水平或垂直线段可以移动。");
        }

        var segments = CloneSegments(map.Segments);
        RepairOrthogonality(points, segments, segmentId);
        return Commit(map, points, segments);
    }

    private FactoryMapMovementResult MoveCollinearChannel(
        FactoryMapDeviceViewData map,
        string selectedSegmentId,
        bool horizontal,
        double perpendicularDelta,
        bool snapToGrid,
        double gridSize)
    {
        var points = ClonePoints(map.ConnectionPoints);
        var segments = CloneSegments(map.Segments);
        var pointById = points.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
        var segmentById = segments.ToDictionary(segment => segment.Id, StringComparer.OrdinalIgnoreCase);
        if (!segmentById.TryGetValue(selectedSegmentId, out var selected)
            || !pointById.TryGetValue(selected.FromPointId, out var selectedFrom)
            || !pointById.TryGetValue(selected.ToPointId, out var selectedTo))
        {
            return FactoryMapMovementResult.Failed("需要移动的线段不存在。");
        }

        var originalAxis = horizontal ? selectedFrom.Y : selectedFrom.X;
        var originalMidpoint = horizontal
            ? (selectedFrom.X + selectedTo.X) / 2
            : (selectedFrom.Y + selectedTo.Y) / 2;
        var targetAxis = ApplyTarget(originalAxis + perpendicularDelta, snapToGrid, gridSize);
        if (NearlyEqual(originalAxis, targetAxis))
        {
            return FactoryMapMovementResult.Succeeded();
        }

        var channelSegmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { selectedSegmentId };
        var queue = new Queue<string>();
        queue.Enqueue(selectedSegmentId);
        while (queue.Count > 0)
        {
            var current = segmentById[queue.Dequeue()];
            foreach (var pointId in new[] { current.FromPointId, current.ToPointId })
            {
                if (!pointById.TryGetValue(pointId, out var point)
                    || point.Kind is not (FactoryMapConnectionPointKinds.Bend or FactoryMapConnectionPointKinds.Junction))
                {
                    continue;
                }

                foreach (var candidate in segments.Where(segment => ReferencesPoint(segment, pointId)))
                {
                    if (channelSegmentIds.Contains(candidate.Id)
                        || !pointById.TryGetValue(candidate.FromPointId, out var from)
                        || !pointById.TryGetValue(candidate.ToPointId, out var to))
                    {
                        continue;
                    }

                    var sameOrientation = horizontal
                        ? NearlyEqual(from.Y, to.Y) && NearlyEqual(from.Y, originalAxis)
                        : NearlyEqual(from.X, to.X) && NearlyEqual(from.X, originalAxis);
                    if (sameOrientation && channelSegmentIds.Add(candidate.Id))
                    {
                        queue.Enqueue(candidate.Id);
                    }
                }
            }
        }

        var channelSegments = segments.Where(segment => channelSegmentIds.Contains(segment.Id)).ToArray();
        var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in channelSegments)
        {
            AddNeighbor(adjacency, segment.FromPointId, segment.ToPointId);
            AddNeighbor(adjacency, segment.ToPointId, segment.FromPointId);
        }

        if (adjacency.Any(pair => pair.Value.Count > 2))
        {
            return FactoryMapMovementResult.Failed("主干通道存在分叉，无法作为单一通道移动。");
        }

        var startId = adjacency.FirstOrDefault(pair => pair.Value.Count == 1).Key;
        if (string.IsNullOrWhiteSpace(startId))
        {
            return FactoryMapMovementResult.Failed("闭合通道暂不支持整体移动。");
        }

        var orderedIds = new List<string>();
        string? previous = null;
        var currentId = startId;
        while (true)
        {
            orderedIds.Add(currentId);
            var next = adjacency[currentId].FirstOrDefault(id =>
                !string.Equals(id, previous, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(next))
            {
                break;
            }

            previous = currentId;
            currentId = next;
        }

        var pathIds = new List<string>();
        var first = pointById[orderedIds[0]];
        MoveChannelPointOrCreateBoundaryBend(
            points,
            pathIds,
            first,
            horizontal,
            targetAxis,
            beforeInternalPoints: true);

        foreach (var pointId in orderedIds.Skip(1).SkipLast(1))
        {
            var point = pointById[pointId];
            SetPerpendicularAxis(point, horizontal, targetAxis);
            pathIds.Add(point.Id);
        }

        var last = pointById[orderedIds[^1]];
        MoveChannelPointOrCreateBoundaryBend(
            points,
            pathIds,
            last,
            horizontal,
            targetAxis,
            beforeInternalPoints: false);

        segments.RemoveAll(segment => channelSegmentIds.Contains(segment.Id));
        var zIndex = channelSegments.Max(segment => segment.ZIndex);
        var updatedPointById = points.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
        var replacementPairs = Enumerable.Range(0, Math.Max(0, pathIds.Count - 1))
            .Where(index => !string.Equals(pathIds[index], pathIds[index + 1], StringComparison.OrdinalIgnoreCase))
            .Select(index => (FromId: pathIds[index], ToId: pathIds[index + 1]))
            .ToArray();
        var preservedPair = replacementPairs
            .Where(pair =>
            {
                var fromPoint = updatedPointById[pair.FromId];
                var toPoint = updatedPointById[pair.ToId];
                return horizontal
                    ? NearlyEqual(fromPoint.Y, targetAxis) && NearlyEqual(toPoint.Y, targetAxis)
                    : NearlyEqual(fromPoint.X, targetAxis) && NearlyEqual(toPoint.X, targetAxis);
            })
            .OrderBy(pair =>
            {
                var fromPoint = updatedPointById[pair.FromId];
                var toPoint = updatedPointById[pair.ToId];
                var midpoint = horizontal
                    ? (fromPoint.X + toPoint.X) / 2
                    : (fromPoint.Y + toPoint.Y) / 2;
                return Math.Abs(midpoint - originalMidpoint);
            })
            .Select(pair => ((string FromId, string ToId)?)pair)
            .FirstOrDefault();

        foreach (var pair in replacementPairs)
        {
            segments.Add(new FactoryMapSegment
            {
                Id = preservedPair is { } preserved
                    && string.Equals(pair.FromId, preserved.FromId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(pair.ToId, preserved.ToId, StringComparison.OrdinalIgnoreCase)
                        ? selectedSegmentId
                        : $"segment-{Guid.NewGuid():N}",
                FromPointId = pair.FromId,
                ToPointId = pair.ToId,
                ZIndex = zIndex
            });
        }

        RepairOrthogonality(points, segments, selectedSegmentId);
        return Commit(map, points, segments);
    }

    private static void MoveChannelPointOrCreateBoundaryBend(
        List<FactoryMapConnectionPoint> points,
        List<string> pathIds,
        FactoryMapConnectionPoint boundary,
        bool horizontal,
        double targetAxis,
        bool beforeInternalPoints)
    {
        pathIds.Add(boundary.Id);
        if (boundary.Kind is FactoryMapConnectionPointKinds.Bend or FactoryMapConnectionPointKinds.Junction)
        {
            SetPerpendicularAxis(boundary, horizontal, targetAxis);
            return;
        }

        var bend = new FactoryMapConnectionPoint
        {
            Id = $"bend-{Guid.NewGuid():N}",
            Kind = FactoryMapConnectionPointKinds.Bend,
            X = horizontal ? boundary.X : targetAxis,
            Y = horizontal ? targetAxis : boundary.Y
        };
        points.Add(bend);
        if (beforeInternalPoints)
        {
            pathIds.Add(bend.Id);
        }
        else
        {
            pathIds.Insert(pathIds.Count - 1, bend.Id);
        }
    }

    private static void SetPerpendicularAxis(
        FactoryMapConnectionPoint point,
        bool horizontal,
        double targetAxis)
    {
        if (horizontal)
        {
            point.Y = targetAxis;
        }
        else
        {
            point.X = targetAxis;
        }
    }

    private static void AddNeighbor(
        IDictionary<string, List<string>> adjacency,
        string pointId,
        string neighborId)
    {
        if (!adjacency.TryGetValue(pointId, out var neighbors))
        {
            neighbors = [];
            adjacency[pointId] = neighbors;
        }

        neighbors.Add(neighborId);
    }

    private FactoryMapMovementResult Commit(
        FactoryMapDeviceViewData map,
        List<FactoryMapConnectionPoint> points,
        List<FactoryMapSegment> segments)
    {
        var candidate = CreateCandidate(map, points, segments);
        var placementError = ValidateChangedSegmentsOutsideDevices(map, candidate);
        if (placementError is not null)
        {
            return FactoryMapMovementResult.Failed(placementError);
        }

        var validation = topologyService.ValidateTopology(candidate);
        if (!validation.IsValid)
        {
            return FactoryMapMovementResult.Failed(string.Join(Environment.NewLine, validation.Errors));
        }

        map.ConnectionPoints = points;
        map.Segments = segments;
        return FactoryMapMovementResult.Succeeded();
    }

    private static string? ValidateChangedSegmentsOutsideDevices(
        FactoryMapDeviceViewData original,
        FactoryMapDeviceViewData candidate)
    {
        if (candidate.Devices.Count == 0 || candidate.Segments.Count == 0)
        {
            return null;
        }

        var originalSegments = original.Segments.ToDictionary(segment => segment.Id, StringComparer.OrdinalIgnoreCase);
        var originalPoints = original.ConnectionPoints.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
        var candidatePoints = candidate.ConnectionPoints.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var segment in candidate.Segments)
        {
            if (!candidatePoints.TryGetValue(segment.FromPointId, out var from)
                || !candidatePoints.TryGetValue(segment.ToPointId, out var to)
                || !HasSegmentGeometryChanged(segment, from, to, originalSegments, originalPoints))
            {
                continue;
            }

            foreach (var device in candidate.Devices)
            {
                if (IsOutwardAttachedStub(from, to, device)
                    || IsOutwardAttachedStub(to, from, device))
                {
                    continue;
                }

                if (IntersectsNodeInteriorOrBoundary(from, to, device))
                {
                    return $"线段移动后会进入节点“{device.Name}”区域，已取消移动。";
                }
            }
        }

        return null;
    }

    private static bool HasSegmentGeometryChanged(
        FactoryMapSegment candidate,
        FactoryMapConnectionPoint candidateFrom,
        FactoryMapConnectionPoint candidateTo,
        IReadOnlyDictionary<string, FactoryMapSegment> originalSegments,
        IReadOnlyDictionary<string, FactoryMapConnectionPoint> originalPoints)
    {
        if (!originalSegments.TryGetValue(candidate.Id, out var original)
            || !originalPoints.TryGetValue(original.FromPointId, out var originalFrom)
            || !originalPoints.TryGetValue(original.ToPointId, out var originalTo))
        {
            return true;
        }

        return !NearlyEqual(candidateFrom.X, originalFrom.X)
            || !NearlyEqual(candidateFrom.Y, originalFrom.Y)
            || !NearlyEqual(candidateTo.X, originalTo.X)
            || !NearlyEqual(candidateTo.Y, originalTo.Y);
    }

    private static bool IsOutwardAttachedStub(
        FactoryMapConnectionPoint attached,
        FactoryMapConnectionPoint other,
        FactoryMapDeviceViewNode device)
    {
        if (attached.Kind != FactoryMapConnectionPointKinds.Attached
            || !string.Equals(attached.OwnerNodeId, device.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return FactoryMapEndpointGeometryService.NormalizePort(attached.Side) switch
        {
            FactoryMapPortKinds.Top => NearlyEqual(attached.X, other.X) && other.Y < attached.Y,
            FactoryMapPortKinds.Right => NearlyEqual(attached.Y, other.Y) && other.X > attached.X,
            FactoryMapPortKinds.Bottom => NearlyEqual(attached.X, other.X) && other.Y > attached.Y,
            FactoryMapPortKinds.Left => NearlyEqual(attached.Y, other.Y) && other.X < attached.X,
            _ => false
        };
    }

    private static bool IntersectsNodeInteriorOrBoundary(
        FactoryMapConnectionPoint from,
        FactoryMapConnectionPoint to,
        FactoryMapDeviceViewNode device)
    {
        const double epsilon = 0.001;
        var left = device.X;
        var top = device.Y;
        var right = left + FactoryMapNodeGeometryService.GetWidth(device);
        var bottom = top + FactoryMapNodeGeometryService.GetHeight(device);
        if (NearlyEqual(from.Y, to.Y))
        {
            var overlap = Math.Min(Math.Max(from.X, to.X), right)
                - Math.Max(Math.Min(from.X, to.X), left);
            return overlap > epsilon
                && from.Y >= top - epsilon
                && from.Y <= bottom + epsilon;
        }

        var verticalOverlap = Math.Min(Math.Max(from.Y, to.Y), bottom)
            - Math.Max(Math.Min(from.Y, to.Y), top);
        return verticalOverlap > epsilon
            && from.X >= left - epsilon
            && from.X <= right + epsilon;
    }

    private static FactoryMapDeviceViewData CreateCandidate(
        FactoryMapDeviceViewData map,
        List<FactoryMapConnectionPoint> points,
        List<FactoryMapSegment> segments)
    {
        return CreateCandidate(map.Devices, points, segments);
    }

    private static FactoryMapDeviceViewData CreateCandidate(
        List<FactoryMapDeviceViewNode> devices,
        List<FactoryMapConnectionPoint> points,
        List<FactoryMapSegment> segments)
    {
        return new FactoryMapDeviceViewData
        {
            Devices = devices,
            ConnectionPoints = points,
            Segments = segments
        };
    }

    private static void UpdateAttachedPoints(
        IEnumerable<FactoryMapConnectionPoint> points,
        FactoryMapDeviceViewNode device,
        double x,
        double y)
    {
        var width = FactoryMapNodeGeometryService.GetWidth(device);
        var height = FactoryMapNodeGeometryService.GetHeight(device);
        foreach (var point in points.Where(point =>
                     point.Kind == FactoryMapConnectionPointKinds.Attached
                     && string.Equals(point.OwnerNodeId, device.Id, StringComparison.OrdinalIgnoreCase)))
        {
            switch (FactoryMapEndpointGeometryService.NormalizePort(point.Side))
            {
                case FactoryMapPortKinds.Top:
                    point.X = x + width / 2;
                    point.Y = y;
                    break;
                case FactoryMapPortKinds.Right:
                    point.X = x + width;
                    point.Y = y + height / 2;
                    break;
                case FactoryMapPortKinds.Bottom:
                    point.X = x + width / 2;
                    point.Y = y + height;
                    break;
                case FactoryMapPortKinds.Left:
                    point.X = x;
                    point.Y = y + height / 2;
                    break;
            }
        }
    }

    private static void RepairOrthogonality(
        List<FactoryMapConnectionPoint> points,
        List<FactoryMapSegment> segments,
        string? preferredSegmentId = null)
    {
        var pointById = points.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var segment in segments.ToList())
        {
            if (!pointById.TryGetValue(segment.FromPointId, out var from)
                || !pointById.TryGetValue(segment.ToPointId, out var to)
                || NearlyEqual(from.X, to.X)
                || NearlyEqual(from.Y, to.Y))
            {
                continue;
            }

            var bend = new FactoryMapConnectionPoint
            {
                Id = $"bend-{Guid.NewGuid():N}",
                Kind = FactoryMapConnectionPointKinds.Bend,
                X = to.X,
                Y = from.Y
            };
            points.Add(bend);
            pointById[bend.Id] = bend;
            segments.Remove(segment);
            segments.Add(new FactoryMapSegment
            {
                Id = $"segment-{Guid.NewGuid():N}",
                FromPointId = from.Id,
                ToPointId = bend.Id,
                ZIndex = segment.ZIndex
            });
            segments.Add(new FactoryMapSegment
            {
                Id = $"segment-{Guid.NewGuid():N}",
                FromPointId = bend.Id,
                ToPointId = to.Id,
                ZIndex = segment.ZIndex
            });
        }

        CollapseCollinearBends(points, segments, preferredSegmentId);
    }

    private static void CollapseCollinearBends(
        List<FactoryMapConnectionPoint> points,
        List<FactoryMapSegment> segments,
        string? preferredSegmentId)
    {
        while (true)
        {
            var pointById = points.ToDictionary(point => point.Id, StringComparer.OrdinalIgnoreCase);
            FactoryMapConnectionPoint? removable = null;
            FactoryMapSegment[] incident = [];
            FactoryMapConnectionPoint? firstNeighbor = null;
            FactoryMapConnectionPoint? secondNeighbor = null;
            foreach (var bend in points.Where(point => point.Kind == FactoryMapConnectionPointKinds.Bend))
            {
                var candidates = segments
                    .Where(segment => ReferencesPoint(segment, bend.Id))
                    .ToArray();
                if (candidates.Length != 2)
                {
                    continue;
                }

                var firstId = GetOtherPointId(candidates[0], bend.Id);
                var secondId = GetOtherPointId(candidates[1], bend.Id);
                if (string.Equals(firstId, secondId, StringComparison.OrdinalIgnoreCase)
                    || !pointById.TryGetValue(firstId, out var first)
                    || !pointById.TryGetValue(secondId, out var second)
                    || (NearlyEqual(first.X, second.X) && NearlyEqual(first.Y, second.Y))
                    || !((NearlyEqual(first.X, bend.X) && NearlyEqual(bend.X, second.X))
                        || (NearlyEqual(first.Y, bend.Y) && NearlyEqual(bend.Y, second.Y))))
                {
                    continue;
                }

                removable = bend;
                incident = candidates;
                firstNeighbor = first;
                secondNeighbor = second;
                break;
            }

            if (removable is null || firstNeighbor is null || secondNeighbor is null)
            {
                return;
            }

            points.Remove(removable);
            segments.Remove(incident[0]);
            segments.Remove(incident[1]);
            var preservePreferredId = !string.IsNullOrWhiteSpace(preferredSegmentId)
                && incident.Any(segment =>
                    string.Equals(segment.Id, preferredSegmentId, StringComparison.OrdinalIgnoreCase));
            var existing = segments.FirstOrDefault(segment =>
                (string.Equals(segment.FromPointId, firstNeighbor.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(segment.ToPointId, secondNeighbor.Id, StringComparison.OrdinalIgnoreCase))
                || (string.Equals(segment.FromPointId, secondNeighbor.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(segment.ToPointId, firstNeighbor.Id, StringComparison.OrdinalIgnoreCase)));
            if (existing is null)
            {
                segments.Add(new FactoryMapSegment
                {
                    Id = preservePreferredId
                        ? preferredSegmentId!
                        : $"segment-{Guid.NewGuid():N}",
                    FromPointId = firstNeighbor.Id,
                    ToPointId = secondNeighbor.Id,
                    ZIndex = Math.Max(incident[0].ZIndex, incident[1].ZIndex)
                });
            }
            else if (preservePreferredId
                     && !segments.Any(segment =>
                         string.Equals(segment.Id, preferredSegmentId, StringComparison.OrdinalIgnoreCase)))
            {
                existing.Id = preferredSegmentId!;
            }
        }
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

    private static List<FactoryMapDeviceViewNode> CloneDevices(IEnumerable<FactoryMapDeviceViewNode> devices)
    {
        return devices.Select(device => new FactoryMapDeviceViewNode
        {
            Id = device.Id,
            Key = device.Key,
            Name = device.Name,
            X = device.X,
            Y = device.Y,
            Width = device.Width,
            Height = device.Height,
            Shortcut = device.Shortcut
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

    private static double ApplyTarget(double value, bool snapToGrid, double gridSize)
    {
        var clamped = Math.Max(0, value);
        return snapToGrid
            ? Math.Round(clamped / gridSize, MidpointRounding.AwayFromZero) * gridSize
            : clamped;
    }

    private static bool NearlyEqual(double first, double second)
    {
        return Math.Abs(first - second) < 0.001;
    }
}
