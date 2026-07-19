using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapLineArrangementServiceTests
{
    private readonly FactoryMapLineArrangementService service = new();

    [Fact]
    public void Arrangement_service_does_not_expose_full_map_arrangement()
    {
        Assert.DoesNotContain(
            typeof(FactoryMapLineArrangementService).GetMethods(),
            method => string.Equals(method.Name, "ArrangeAll", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrangeConnectedTo_empty_anchor_set_does_not_modify_any_route()
    {
        var map = CreateBadScreenshotMap();
        var snapshot = Snapshot(map);

        var result = service.ArrangeConnectedTo(map, [], 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(snapshot, Snapshot(map));
    }

    [Fact]
    public void ArrangeConnectedTo_failure_keeps_original_topology_unchanged()
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

        var result = service.ArrangeConnectedTo(map, ["node:top"], 10);

        Assert.False(result.Success);
        Assert.Equal(snapshot, Snapshot(map));
    }

    [Fact]
    public void ArrangeConnectedTo_empty_map_reports_no_work()
    {
        var map = new FactoryMapDeviceViewData();

        var result = service.ArrangeConnectedTo(map, ["missing"], 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Empty(map.ConnectionPoints);
        Assert.Empty(map.Segments);
    }

    [Fact]
    public void ArrangeConnectedTo_normalizes_legacy_off_grid_escape_channel()
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

        var result = service.ArrangeConnectedTo(map, ["source:bottom"], 10);

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

    private static string Snapshot(FactoryMapDeviceViewData map)
    {
        return string.Join("|", map.ConnectionPoints.OrderBy(point => point.Id)
            .Select(point => $"P:{point.Id}:{point.Kind}:{point.X}:{point.Y}"))
            + "#"
            + string.Join("|", map.Segments.OrderBy(segment => segment.Id)
                .Select(segment => $"S:{segment.Id}:{segment.FromPointId}:{segment.ToPointId}:{segment.ZIndex}"));
    }
}
