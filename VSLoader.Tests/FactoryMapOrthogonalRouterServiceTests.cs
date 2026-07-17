using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapOrthogonalRouterServiceTests
{
    private readonly FactoryMapOrthogonalRouterService service = new();

    [Fact]
    public void Route_top_to_left_leaves_and_enters_perpendicular_without_hugging_node_edges()
    {
        var map = CreateScreenshotMap();
        var from = GetPoint(map, "bottom:top");
        var to = GetPoint(map, "right:left");

        var result = service.Route(map, from, to, 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.Points.Count >= 3);
        Assert.Equal(from.X, result.Points[0].X);
        Assert.Equal(from.Y, result.Points[0].Y);
        Assert.Equal(from.X, result.Points[1].X);
        Assert.True(result.Points[1].Y < from.Y);
        Assert.Equal(to.X, result.Points[^1].X);
        Assert.Equal(to.Y, result.Points[^1].Y);
        Assert.Equal(to.Y, result.Points[^2].Y);
        Assert.True(result.Points[^2].X < to.X);
        Assert.DoesNotContain(GetSegments(result), segment =>
            IsHorizontalBoundaryOverlap(segment, 100, 300, 250));
        Assert.DoesNotContain(GetSegments(result), segment =>
            IsVerticalBoundaryOverlap(segment, 400, 100, 158));
    }

    [Fact]
    public void Route_reversing_endpoints_produces_the_same_geometry_in_reverse()
    {
        var map = CreateScreenshotMap();
        var from = GetPoint(map, "bottom:top");
        var to = GetPoint(map, "right:left");

        var forward = service.Route(map, from, to, 10);
        var reverse = service.Route(map, to, from, 10);

        Assert.True(forward.Success, forward.ErrorMessage);
        Assert.True(reverse.Success, reverse.ErrorMessage);
        Assert.Equal(
            forward.Points.Select(PointKey),
            reverse.Points.Reverse().Select(PointKey));
    }

    [Fact]
    public void Route_avoids_a_device_between_opposite_ports()
    {
        var map = new FactoryMapDeviceViewData
        {
            Devices =
            [
                new FactoryMapDeviceViewNode { Id = "left", X = 100, Y = 100 },
                new FactoryMapDeviceViewNode { Id = "obstacle", X = 330, Y = 90 },
                new FactoryMapDeviceViewNode { Id = "right", X = 560, Y = 100 }
            ],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "left:right", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "left", Side = FactoryMapPortKinds.Right, X = 260, Y = 130 },
                new FactoryMapConnectionPoint { Id = "right:left", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "right", Side = FactoryMapPortKinds.Left, X = 560, Y = 130 }
            ]
        };

        var result = service.Route(map, GetPoint(map, "left:right"), GetPoint(map, "right:left"), 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.DoesNotContain(GetSegments(result), segment => IntersectsRectangleInterior(segment, 330, 90, 160, 60));
        Assert.True(result.Points.Count >= 6);
    }

    [Fact]
    public void Route_facing_ports_supports_three_grid_gap_with_compact_clearance()
    {
        var map = new FactoryMapDeviceViewData
        {
            Devices =
            [
                new FactoryMapDeviceViewNode { Id = "upper-left", X = 100, Y = 100 },
                new FactoryMapDeviceViewNode { Id = "upper-right", X = 290, Y = 100 },
                new FactoryMapDeviceViewNode { Id = "lower-left", X = 100, Y = 190 }
            ],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint
                {
                    Id = "lower-left:top",
                    Kind = FactoryMapConnectionPointKinds.Attached,
                    OwnerNodeId = "lower-left",
                    Side = FactoryMapPortKinds.Top,
                    X = 180,
                    Y = 190
                },
                new FactoryMapConnectionPoint
                {
                    Id = "upper-right:bottom",
                    Kind = FactoryMapConnectionPointKinds.Attached,
                    OwnerNodeId = "upper-right",
                    Side = FactoryMapPortKinds.Bottom,
                    X = 370,
                    Y = 160
                }
            ]
        };

        var result = service.Route(map, GetPoint(map, "lower-left:top"), GetPoint(map, "upper-right:bottom"), 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(180, result.Points[0].X);
        Assert.Equal(190, result.Points[0].Y);
        Assert.Equal(370, result.Points[^1].X);
        Assert.Equal(160, result.Points[^1].Y);
        Assert.DoesNotContain(GetSegments(result), segment =>
            IntersectsRectangleInterior(segment, 100, 100, 160, 60));
        Assert.DoesNotContain(GetSegments(result), segment =>
            IntersectsRectangleInterior(segment, 290, 100, 160, 60));
        Assert.DoesNotContain(GetSegments(result), segment =>
            IntersectsRectangleInterior(segment, 100, 190, 160, 60));
    }

    [Fact]
    public void Route_free_to_free_uses_shortest_orthogonal_path()
    {
        var map = new FactoryMapDeviceViewData
        {
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "a", Kind = FactoryMapConnectionPointKinds.Free, X = 100, Y = 100 },
                new FactoryMapConnectionPoint { Id = "b", Kind = FactoryMapConnectionPointKinds.Free, X = 300, Y = 200 }
            ]
        };

        var result = service.Route(map, GetPoint(map, "a"), GetPoint(map, "b"), 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(3, result.Points.Count);
        Assert.All(GetSegments(result), segment =>
            Assert.True(segment.Start.X == segment.End.X || segment.Start.Y == segment.End.Y));
    }

    [Fact]
    public void Route_rejects_attached_port_that_cannot_escape_zero_boundary()
    {
        var map = new FactoryMapDeviceViewData
        {
            Devices = [new FactoryMapDeviceViewNode { Id = "node", X = 100, Y = 0 }],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "node:top", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node", Side = FactoryMapPortKinds.Top, X = 175, Y = 0 },
                new FactoryMapConnectionPoint { Id = "free", Kind = FactoryMapConnectionPointKinds.Free, X = 300, Y = 100 }
            ]
        };

        var result = service.Route(map, GetPoint(map, "node:top"), GetPoint(map, "free"), 10);

        Assert.False(result.Success);
        Assert.Contains("边界", result.ErrorMessage);
        Assert.Empty(result.Points);
    }

    [Fact]
    public void Route_bottom_port_snaps_escape_outward_to_the_map_grid()
    {
        var map = new FactoryMapDeviceViewData
        {
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
                }
            ]
        };

        var result = service.Route(map, GetPoint(map, "source:bottom"), GetPoint(map, "target:top"), 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(935, result.Points[1].X);
        Assert.Equal(630, result.Points[1].Y);
        Assert.DoesNotContain(result.Points, point => point.Y == 628);
    }

    private static FactoryMapDeviceViewData CreateScreenshotMap()
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
                new FactoryMapConnectionPoint { Id = "right:left", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "right", Side = FactoryMapPortKinds.Left, X = 400, Y = 129 }
            ]
        };
    }

    private static FactoryMapConnectionPoint GetPoint(FactoryMapDeviceViewData map, string id)
    {
        return map.ConnectionPoints.Single(point => point.Id == id);
    }

    private static IReadOnlyList<(FactoryMapPoint Start, FactoryMapPoint End)> GetSegments(FactoryMapRouteResult result)
    {
        return result.Points.Zip(result.Points.Skip(1), (start, end) => (start, end)).ToArray();
    }

    private static bool IsHorizontalBoundaryOverlap(
        (FactoryMapPoint Start, FactoryMapPoint End) segment,
        double left,
        double y,
        double right)
    {
        return segment.Start.Y == y
            && segment.End.Y == y
            && Math.Min(segment.Start.X, segment.End.X) < right
            && Math.Max(segment.Start.X, segment.End.X) > left;
    }

    private static bool IsVerticalBoundaryOverlap(
        (FactoryMapPoint Start, FactoryMapPoint End) segment,
        double x,
        double top,
        double bottom)
    {
        return segment.Start.X == x
            && segment.End.X == x
            && Math.Min(segment.Start.Y, segment.End.Y) < bottom
            && Math.Max(segment.Start.Y, segment.End.Y) > top;
    }

    private static bool IntersectsRectangleInterior(
        (FactoryMapPoint Start, FactoryMapPoint End) segment,
        double left,
        double top,
        double width,
        double height)
    {
        var right = left + width;
        var bottom = top + height;
        if (segment.Start.Y == segment.End.Y)
        {
            return segment.Start.Y > top
                && segment.Start.Y < bottom
                && Math.Max(Math.Min(segment.Start.X, segment.End.X), left)
                    < Math.Min(Math.Max(segment.Start.X, segment.End.X), right);
        }

        return segment.Start.X > left
            && segment.Start.X < right
            && Math.Max(Math.Min(segment.Start.Y, segment.End.Y), top)
                < Math.Min(Math.Max(segment.Start.Y, segment.End.Y), bottom);
    }

    private static string PointKey(FactoryMapPoint point) => $"{point.X:R},{point.Y:R}";
}
