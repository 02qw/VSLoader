using VSLoader.Models;

namespace VSLoader.Services;

public sealed class FactoryMapConnectionDraftService
{
    private readonly FactoryMapTopologyService topologyService = new();

    public FactoryMapTopologyOperationResult CompleteToPoint(
        FactoryMapDeviceViewData map,
        FactoryMapConnectionDraft draft,
        string targetPointId,
        double gridSize,
        double endpointThreshold)
    {
        if (string.IsNullOrWhiteSpace(targetPointId))
        {
            return FactoryMapTopologyOperationResult.Failed("连接终点不存在。");
        }

        var candidate = CloneMap(map);
        string sourcePointId;
        if (draft.OriginKind == FactoryMapConnectionOriginKinds.Point)
        {
            sourcePointId = draft.PointId;
        }
        else if (draft.OriginKind == FactoryMapConnectionOriginKinds.Segment)
        {
            var split = topologyService.SplitSegmentWithJunctionAt(
                candidate,
                draft.SegmentId,
                draft.SegmentX,
                draft.SegmentY,
                gridSize,
                endpointThreshold);
            if (!split.Success || string.IsNullOrWhiteSpace(split.PointId))
            {
                return FactoryMapTopologyOperationResult.Failed(split.ErrorMessage ?? "分支起点创建失败。");
            }

            sourcePointId = split.PointId;
        }
        else
        {
            return FactoryMapTopologyOperationResult.Failed("连接草稿类型无效。");
        }

        var connected = topologyService.ConnectPoints(candidate, sourcePointId, targetPointId);
        if (!connected.Success)
        {
            return connected;
        }

        Commit(map, candidate);
        return FactoryMapTopologyOperationResult.Succeeded(sourcePointId);
    }

    public FactoryMapTopologyOperationResult CompleteToSegment(
        FactoryMapDeviceViewData map,
        FactoryMapConnectionDraft draft,
        string targetSegmentId,
        double targetX,
        double targetY,
        double gridSize,
        double endpointThreshold)
    {
        if (string.IsNullOrWhiteSpace(targetSegmentId))
        {
            return FactoryMapTopologyOperationResult.Failed("连接终点线段不存在。");
        }

        if (draft.OriginKind == FactoryMapConnectionOriginKinds.Segment
            && string.Equals(draft.SegmentId, targetSegmentId, StringComparison.OrdinalIgnoreCase))
        {
            return FactoryMapTopologyOperationResult.Failed("暂不支持同一线段上的两个位置互相连接。");
        }

        var candidate = CloneMap(map);
        string sourcePointId;
        if (draft.OriginKind == FactoryMapConnectionOriginKinds.Point)
        {
            sourcePointId = draft.PointId;
        }
        else if (draft.OriginKind == FactoryMapConnectionOriginKinds.Segment)
        {
            var sourceSplit = topologyService.SplitSegmentWithJunctionAt(
                candidate,
                draft.SegmentId,
                draft.SegmentX,
                draft.SegmentY,
                gridSize,
                endpointThreshold);
            if (!sourceSplit.Success || string.IsNullOrWhiteSpace(sourceSplit.PointId))
            {
                return FactoryMapTopologyOperationResult.Failed(sourceSplit.ErrorMessage ?? "分支起点创建失败。");
            }

            sourcePointId = sourceSplit.PointId;
        }
        else
        {
            return FactoryMapTopologyOperationResult.Failed("连接草稿类型无效。");
        }

        var targetSplit = topologyService.SplitSegmentWithJunctionAt(
            candidate,
            targetSegmentId,
            targetX,
            targetY,
            gridSize,
            endpointThreshold);
        if (!targetSplit.Success || string.IsNullOrWhiteSpace(targetSplit.PointId))
        {
            return FactoryMapTopologyOperationResult.Failed(targetSplit.ErrorMessage ?? "分支终点创建失败。");
        }

        var connected = topologyService.ConnectPoints(candidate, sourcePointId, targetSplit.PointId);
        if (!connected.Success)
        {
            return connected;
        }

        Commit(map, candidate);
        return FactoryMapTopologyOperationResult.Succeeded(targetSplit.PointId);
    }

    private static FactoryMapDeviceViewData CloneMap(FactoryMapDeviceViewData map)
    {
        return new FactoryMapDeviceViewData
        {
            TopologyAuthoritative = true,
            Devices = map.Devices,
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

    private static void Commit(FactoryMapDeviceViewData map, FactoryMapDeviceViewData candidate)
    {
        map.ConnectionPoints = candidate.ConnectionPoints;
        map.Segments = candidate.Segments;
    }
}
