using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapLogicalRouteServiceTests
{
    private readonly FactoryMapLogicalRouteService service = new();

    [Fact]
    public void Enumerate_finds_maximal_anchor_to_anchor_bend_chains_once()
    {
        var map = new FactoryMapDeviceViewData
        {
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "a", Kind = FactoryMapConnectionPointKinds.Free, X = 100, Y = 100 },
                new FactoryMapConnectionPoint { Id = "bend-1", Kind = FactoryMapConnectionPointKinds.Bend, X = 200, Y = 100 },
                new FactoryMapConnectionPoint { Id = "bend-2", Kind = FactoryMapConnectionPointKinds.Bend, X = 200, Y = 200 },
                new FactoryMapConnectionPoint { Id = "junction", Kind = FactoryMapConnectionPointKinds.Junction, JunctionAxis = FactoryMapJunctionAxes.Horizontal, X = 300, Y = 200 },
                new FactoryMapConnectionPoint { Id = "c", Kind = FactoryMapConnectionPointKinds.Free, X = 400, Y = 200 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "s1", FromPointId = "a", ToPointId = "bend-1" },
                new FactoryMapSegment { Id = "s2", FromPointId = "bend-1", ToPointId = "bend-2" },
                new FactoryMapSegment { Id = "s3", FromPointId = "bend-2", ToPointId = "junction" },
                new FactoryMapSegment { Id = "s4", FromPointId = "junction", ToPointId = "c" }
            ]
        };

        var result = service.Enumerate(map);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, result.Routes.Count);
        var first = result.Routes.Single(route => route.StartPointId == "a" || route.EndPointId == "a");
        Assert.Equal(["bend-1", "bend-2"], first.BendPointIds);
        Assert.Equal(3, first.SegmentIds.Count);
        Assert.Equal(4, result.Routes.Sum(route => route.SegmentIds.Count));
    }

    [Fact]
    public void Enumerate_rejects_bend_only_cycle()
    {
        var map = new FactoryMapDeviceViewData
        {
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "a", Kind = FactoryMapConnectionPointKinds.Bend, X = 100, Y = 100 },
                new FactoryMapConnectionPoint { Id = "b", Kind = FactoryMapConnectionPointKinds.Bend, X = 200, Y = 100 },
                new FactoryMapConnectionPoint { Id = "c", Kind = FactoryMapConnectionPointKinds.Bend, X = 200, Y = 200 },
                new FactoryMapConnectionPoint { Id = "d", Kind = FactoryMapConnectionPointKinds.Bend, X = 100, Y = 200 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "s1", FromPointId = "a", ToPointId = "b" },
                new FactoryMapSegment { Id = "s2", FromPointId = "b", ToPointId = "c" },
                new FactoryMapSegment { Id = "s3", FromPointId = "c", ToPointId = "d" },
                new FactoryMapSegment { Id = "s4", FromPointId = "d", ToPointId = "a" }
            ]
        };

        var result = service.Enumerate(map);

        Assert.False(result.Success);
        Assert.Contains("闭环", result.ErrorMessage);
    }
}
