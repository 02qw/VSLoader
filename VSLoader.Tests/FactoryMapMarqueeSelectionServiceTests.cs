using System.Windows;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapMarqueeSelectionServiceTests
{
    private readonly FactoryMapMarqueeSelectionService service = new();

    [Fact]
    public void Selects_intersecting_devices_and_free_points_only()
    {
        var map = CreateMap();

        var selected = service.GetSelection(
            map,
            new Rect(90, 90, 360, 180),
            pointSize: 10);

        Assert.Contains(new FactoryMapObjectRef(FactoryMapObjectKind.Device, "node-a"), selected);
        Assert.Contains(new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "free-1"), selected);
        Assert.DoesNotContain(new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "node-a:right"), selected);
        Assert.DoesNotContain(new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "bend-1"), selected);
        Assert.DoesNotContain(new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "segment-1"), selected);
    }

    [Fact]
    public void Free_point_circle_touching_selection_boundary_is_included()
    {
        var map = CreateMap();

        var selected = service.GetSelection(
            map,
            new Rect(405, 215, 20, 20),
            pointSize: 10);

        Assert.Contains(new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "free-1"), selected);
    }

    private static FactoryMapDeviceViewData CreateMap()
    {
        return new FactoryMapDeviceViewData
        {
            Devices =
            [
                new FactoryMapDeviceViewNode { Id = "node-a", X = 100, Y = 100 },
                new FactoryMapDeviceViewNode { Id = "node-b", X = 800, Y = 800 }
            ],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "node-a:right", Kind = FactoryMapConnectionPointKinds.Attached, X = 250, Y = 129 },
                new FactoryMapConnectionPoint { Id = "free-1", Kind = FactoryMapConnectionPointKinds.Free, X = 410, Y = 220 },
                new FactoryMapConnectionPoint { Id = "bend-1", Kind = FactoryMapConnectionPointKinds.Bend, X = 300, Y = 220 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "segment-1", FromPointId = "node-a:right", ToPointId = "free-1" }
            ]
        };
    }
}
