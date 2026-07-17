using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapRenderIndexServiceTests
{
    private readonly FactoryMapRenderIndexService service = new();

    [Fact]
    public void Build_splits_overlap_intervals_and_draws_each_coordinate_once()
    {
        var points = new[]
        {
            Point("a", 0, 20),
            Point("b", 100, 20),
            Point("c", 50, 20),
            Point("d", 150, 20)
        };
        var segments = new[]
        {
            Segment("s1", "a", "b", 1),
            Segment("s2", "c", "d", 2)
        };

        var visible = service.Build(points, segments);

        Assert.Equal(3, visible.Count);
        Assert.Contains(visible, item => item.Start.X == 0 && item.End.X == 50 && item.SourceSegmentIds.SequenceEqual(["s1"]));
        Assert.Contains(visible, item => item.Start.X == 50 && item.End.X == 100 && item.SourceSegmentIds.Count == 2);
        Assert.Contains(visible, item => item.Start.X == 100 && item.End.X == 150 && item.SourceSegmentIds.SequenceEqual(["s2"]));
    }

    [Fact]
    public void Build_uses_selected_segment_then_zindex_for_top_segment()
    {
        var points = new[]
        {
            Point("a", 0, 20),
            Point("b", 100, 20),
            Point("c", 0, 20),
            Point("d", 100, 20)
        };
        var segments = new[]
        {
            Segment("low", "a", "b", 1),
            Segment("high", "c", "d", 10)
        };

        Assert.Equal("high", Assert.Single(service.Build(points, segments)).TopSegmentId);
        Assert.Equal("low", Assert.Single(service.Build(points, segments, "low")).TopSegmentId);
    }

    [Fact]
    public void Build_keeps_perpendicular_crossing_as_two_independent_visible_segments()
    {
        var points = new[]
        {
            Point("left", 0, 50),
            Point("right", 100, 50),
            Point("top", 50, 0),
            Point("bottom", 50, 100)
        };
        var segments = new[]
        {
            Segment("horizontal", "left", "right", 1),
            Segment("vertical", "top", "bottom", 2)
        };

        var visible = service.Build(points, segments);

        Assert.Equal(2, visible.Count);
        Assert.Contains(visible, item => item.SourceSegmentIds.SequenceEqual(["horizontal"]));
        Assert.Contains(visible, item => item.SourceSegmentIds.SequenceEqual(["vertical"]));
    }

    private static FactoryMapConnectionPoint Point(string id, double x, double y)
    {
        return new FactoryMapConnectionPoint { Id = id, Kind = FactoryMapConnectionPointKinds.Free, X = x, Y = y };
    }

    private static FactoryMapSegment Segment(string id, string from, string to, int zIndex)
    {
        return new FactoryMapSegment { Id = id, FromPointId = from, ToPointId = to, ZIndex = zIndex };
    }
}
