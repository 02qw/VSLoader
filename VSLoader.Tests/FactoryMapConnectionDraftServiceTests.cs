using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapConnectionDraftServiceTests
{
    private readonly FactoryMapConnectionDraftService service = new();

    [Fact]
    public void Complete_point_to_segment_creates_junction_and_connection_atomically()
    {
        var map = CreateMap();
        var draft = FactoryMapConnectionDraft.FromPoint("source");

        var result = service.CompleteToSegment(map, draft, "trunk", 350, 129, 10, 10);

        Assert.True(result.Success, result.ErrorMessage);
        var junction = map.ConnectionPoints.Single(point => point.Kind == FactoryMapConnectionPointKinds.Junction);
        Assert.Equal(FactoryMapJunctionAxes.Horizontal, junction.JunctionAxis);
        Assert.Equal(3, map.Segments.Count);
        Assert.Contains(map.Segments, segment => References(segment, "source") && References(segment, junction.Id));
    }

    [Fact]
    public void Complete_segment_to_point_creates_junction_only_when_completed()
    {
        var map = CreateMap();
        var pointCount = map.ConnectionPoints.Count;
        var draft = FactoryMapConnectionDraft.FromSegment("trunk", 350, 129);

        Assert.Equal(pointCount, map.ConnectionPoints.Count);

        var result = service.CompleteToPoint(map, draft, "source", 10, 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Single(map.ConnectionPoints, point => point.Kind == FactoryMapConnectionPointKinds.Junction);
        Assert.Equal(3, map.Segments.Count);
    }

    [Fact]
    public void Complete_same_segment_to_itself_is_rejected_without_mutation()
    {
        var map = CreateMap();
        var originalPoints = map.ConnectionPoints.Count;
        var originalSegments = map.Segments.Count;
        var draft = FactoryMapConnectionDraft.FromSegment("trunk", 320, 129);

        var result = service.CompleteToSegment(map, draft, "trunk", 420, 129, 10, 10);

        Assert.False(result.Success);
        Assert.Equal(originalPoints, map.ConnectionPoints.Count);
        Assert.Equal(originalSegments, map.Segments.Count);
    }

    private static FactoryMapDeviceViewData CreateMap()
    {
        return new FactoryMapDeviceViewData
        {
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "left", Kind = FactoryMapConnectionPointKinds.Free, X = 200, Y = 129 },
                new FactoryMapConnectionPoint { Id = "right", Kind = FactoryMapConnectionPointKinds.Free, X = 500, Y = 129 },
                new FactoryMapConnectionPoint { Id = "source", Kind = FactoryMapConnectionPointKinds.Free, X = 350, Y = 300 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "trunk", FromPointId = "left", ToPointId = "right" }
            ]
        };
    }

    private static bool References(FactoryMapSegment segment, string pointId)
    {
        return segment.FromPointId == pointId || segment.ToPointId == pointId;
    }
}
