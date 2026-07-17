using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapLineArrangementServiceTests
{
    private readonly FactoryMapLineArrangementService service = new();

    [Fact]
    public void ArrangeAll_replaces_edge_hugging_bend_without_moving_anchors()
    {
        var map = CreateBadScreenshotMap();

        var result = service.ArrangeAll(map, 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1, result.ArrangedRouteCount);
        Assert.Equal((175d, 300d), GetPoint(map, "bottom:top"));
        Assert.Equal((400d, 129d), GetPoint(map, "right:left"));
        Assert.DoesNotContain(map.ConnectionPoints, point => point.Id == "bad-bend");
        var sourceSegment = Assert.Single(map.Segments, segment => References(segment, "bottom:top"));
        var sourceNeighbor = GetOtherPoint(map, sourceSegment, "bottom:top");
        Assert.Equal(175, sourceNeighbor.X);
        Assert.True(sourceNeighbor.Y < 300);
        var targetSegment = Assert.Single(map.Segments, segment => References(segment, "right:left"));
        var targetNeighbor = GetOtherPoint(map, targetSegment, "right:left");
        Assert.Equal(129, targetNeighbor.Y);
        Assert.True(targetNeighbor.X < 400);
    }

    [Fact]
    public void ArrangeAll_failure_keeps_original_topology_unchanged()
    {
        var map = new FactoryMapDeviceViewData
        {
            Devices = [new FactoryMapDeviceViewNode { Id = "node", X = 100, Y = 0 }],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "node:top", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node", Side = FactoryMapPortKinds.Top, X = 175, Y = 0 },
                new FactoryMapConnectionPoint { Id = "free", Kind = FactoryMapConnectionPointKinds.Free, X = 300, Y = 100 },
                new FactoryMapConnectionPoint { Id = "bend", Kind = FactoryMapConnectionPointKinds.Bend, X = 300, Y = 0 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "s1", FromPointId = "node:top", ToPointId = "bend" },
                new FactoryMapSegment { Id = "s2", FromPointId = "bend", ToPointId = "free" }
            ]
        };
        var snapshot = Snapshot(map);

        var result = service.ArrangeAll(map, 10);

        Assert.False(result.Success);
        Assert.Equal(snapshot, Snapshot(map));
    }

    [Fact]
    public void ArrangeAll_empty_map_reports_no_work()
    {
        var map = new FactoryMapDeviceViewData();

        var result = service.ArrangeAll(map, 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(0, result.ArrangedRouteCount);
        Assert.Empty(map.ConnectionPoints);
        Assert.Empty(map.Segments);
    }

    [Fact]
    public void ArrangeAll_normalizes_legacy_off_grid_escape_channel()
    {
        var map = new FactoryMapDeviceViewData
        {
            TopologyAuthoritative = true,
            Devices =
            [
                new FactoryMapDeviceViewNode { Id = "source", X = 860, Y = 550 },
                new FactoryMapDeviceViewNode { Id = "target", X = 700, Y = 700 }
            ],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint
                {
                    Id = "source:bottom",
                    Kind = FactoryMapConnectionPointKinds.Attached,
                    OwnerNodeId = "source",
                    Side = FactoryMapPortKinds.Bottom,
                    X = 935,
                    Y = 608
                },
                new FactoryMapConnectionPoint
                {
                    Id = "target:top",
                    Kind = FactoryMapConnectionPointKinds.Attached,
                    OwnerNodeId = "target",
                    Side = FactoryMapPortKinds.Top,
                    X = 775,
                    Y = 700
                },
                new FactoryMapConnectionPoint { Id = "old-a", Kind = FactoryMapConnectionPointKinds.Bend, X = 935, Y = 628 },
                new FactoryMapConnectionPoint { Id = "old-b", Kind = FactoryMapConnectionPointKinds.Bend, X = 775, Y = 628 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "old-1", FromPointId = "source:bottom", ToPointId = "old-a" },
                new FactoryMapSegment { Id = "old-2", FromPointId = "old-a", ToPointId = "old-b" },
                new FactoryMapSegment { Id = "old-3", FromPointId = "old-b", ToPointId = "target:top" }
            ]
        };

        var result = service.ArrangeAll(map, 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.DoesNotContain(map.ConnectionPoints, point => point.Kind == FactoryMapConnectionPointKinds.Bend && point.Y == 628);
        Assert.Contains(map.ConnectionPoints, point => point.Kind == FactoryMapConnectionPointKinds.Bend && point.X == 935 && point.Y == 630);
    }

    private static FactoryMapDeviceViewData CreateBadScreenshotMap()
    {
        return new FactoryMapDeviceViewData
        {
            Devices =
            [
                new FactoryMapDeviceViewNode { Id = "bottom", X = 100, Y = 300 },
                new FactoryMapDeviceViewNode { Id = "right", X = 400, Y = 100 }
            ],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "bottom:top", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "bottom", Side = FactoryMapPortKinds.Top, X = 175, Y = 300 },
                new FactoryMapConnectionPoint { Id = "right:left", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "right", Side = FactoryMapPortKinds.Left, X = 400, Y = 129 },
                new FactoryMapConnectionPoint { Id = "bad-bend", Kind = FactoryMapConnectionPointKinds.Bend, X = 400, Y = 300 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "bad-1", FromPointId = "bottom:top", ToPointId = "bad-bend", ZIndex = 4 },
                new FactoryMapSegment { Id = "bad-2", FromPointId = "bad-bend", ToPointId = "right:left", ZIndex = 5 }
            ]
        };
    }

    private static bool References(FactoryMapSegment segment, string pointId)
    {
        return segment.FromPointId == pointId || segment.ToPointId == pointId;
    }

    private static FactoryMapConnectionPoint GetOtherPoint(
        FactoryMapDeviceViewData map,
        FactoryMapSegment segment,
        string pointId)
    {
        var otherId = segment.FromPointId == pointId ? segment.ToPointId : segment.FromPointId;
        return map.ConnectionPoints.Single(point => point.Id == otherId);
    }

    private static (double X, double Y) GetPoint(FactoryMapDeviceViewData map, string pointId)
    {
        var point = map.ConnectionPoints.Single(candidate => candidate.Id == pointId);
        return (point.X, point.Y);
    }

    private static string Snapshot(FactoryMapDeviceViewData map)
    {
        return string.Join("|", map.ConnectionPoints.OrderBy(point => point.Id)
            .Select(point => $"P:{point.Id}:{point.Kind}:{point.X}:{point.Y}"))
            + "#"
            + string.Join("|", map.Segments.OrderBy(segment => segment.Id)
                .Select(segment => $"S:{segment.Id}:{segment.FromPointId}:{segment.ToPointId}:{segment.ZIndex}"));
    }
}
