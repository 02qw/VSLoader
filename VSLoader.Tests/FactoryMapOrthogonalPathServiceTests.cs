using System.Windows;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapOrthogonalPathServiceTests
{
    [Fact]
    public void Normalize_filters_invalid_points_and_removes_duplicate_points()
    {
        var result = FactoryMapOrthogonalPathService.Normalize(
            new Point(0, 0),
            [
                new FactoryMapPoint { X = double.NaN, Y = 20 },
                new FactoryMapPoint { X = 40, Y = 0 },
                new FactoryMapPoint { X = 40, Y = 0 },
                new FactoryMapPoint { X = 40, Y = double.PositiveInfinity }
            ],
            new Point(80, 0));

        Assert.Empty(result);
    }

    [Fact]
    public void Normalize_inserts_corner_points_for_diagonal_segments()
    {
        var result = FactoryMapOrthogonalPathService.Normalize(
            new Point(0, 0),
            [
                new FactoryMapPoint { X = 40, Y = 30 }
            ],
            new Point(80, 30));

        AssertOrthogonal(CreateFullPath(new Point(0, 0), result, new Point(80, 30)));
        Assert.Equal(2, result.Count);
        AssertPoint(result[0], 40, 0);
        AssertPoint(result[1], 40, 30);
    }

    [Fact]
    public void Normalize_removes_collinear_middle_points()
    {
        var result = FactoryMapOrthogonalPathService.Normalize(
            new Point(0, 0),
            [
                new FactoryMapPoint { X = 20, Y = 0 },
                new FactoryMapPoint { X = 40, Y = 0 },
                new FactoryMapPoint { X = 40, Y = 30 }
            ],
            new Point(40, 60));

        Assert.Single(result);
        AssertPoint(result[0], 40, 0);
        AssertOrthogonal(CreateFullPath(new Point(0, 0), result, new Point(40, 60)));
    }

    [Fact]
    public void InsertDetour_adds_vertical_channel_on_horizontal_segment()
    {
        var result = FactoryMapOrthogonalPathService.InsertDetour(
            new Point(0, 0),
            [],
            new Point(100, 0),
            new Point(45, 35),
            10);

        Assert.Equal(3, result.Count);
        AssertPoint(result[0], 50, 0);
        AssertPoint(result[1], 50, 40);
        AssertPoint(result[2], 100, 40);
        AssertOrthogonal(CreateFullPath(new Point(0, 0), result, new Point(100, 0)));
    }

    [Fact]
    public void InsertDetour_adds_horizontal_channel_on_vertical_segment()
    {
        var result = FactoryMapOrthogonalPathService.InsertDetour(
            new Point(0, 0),
            [],
            new Point(0, 100),
            new Point(35, 45),
            10);

        Assert.Equal(3, result.Count);
        AssertPoint(result[0], 0, 50);
        AssertPoint(result[1], 40, 50);
        AssertPoint(result[2], 40, 100);
        AssertOrthogonal(CreateFullPath(new Point(0, 0), result, new Point(0, 100)));
    }

    [Fact]
    public void InsertDetour_uses_default_offset_when_click_has_no_detour_depth()
    {
        var result = FactoryMapOrthogonalPathService.InsertDetour(
            new Point(0, 0),
            [],
            new Point(100, 0),
            new Point(45, 0),
            10);

        Assert.Equal(3, result.Count);
        AssertPoint(result[0], 50, 0);
        AssertPoint(result[1], 50, 20);
        AssertPoint(result[2], 100, 20);
    }

    [Fact]
    public void GetSegments_returns_directional_segments_for_normalized_path()
    {
        var segments = FactoryMapOrthogonalPathService.GetSegments(
            new Point(0, 0),
            [
                new FactoryMapPoint { X = 40, Y = 0 },
                new FactoryMapPoint { X = 40, Y = 30 }
            ],
            new Point(80, 30));

        Assert.Equal(3, segments.Count);
        Assert.Equal(0, segments[0].SegmentIndex);
        Assert.Equal(FactoryMapEdgeSegmentDirection.Horizontal, segments[0].Direction);
        Assert.Equal(1, segments[1].SegmentIndex);
        Assert.Equal(FactoryMapEdgeSegmentDirection.Vertical, segments[1].Direction);
        Assert.Equal(2, segments[2].SegmentIndex);
        Assert.Equal(FactoryMapEdgeSegmentDirection.Horizontal, segments[2].Direction);
    }

    [Fact]
    public void FindNearestSegmentIndex_returns_segment_closest_to_click_point()
    {
        var index = FactoryMapOrthogonalPathService.FindNearestSegmentIndex(
            new Point(0, 0),
            [
                new FactoryMapPoint { X = 40, Y = 0 },
                new FactoryMapPoint { X = 40, Y = 30 }
            ],
            new Point(80, 30),
            new Point(42, 18));

        Assert.Equal(1, index);
    }

    [Fact]
    public void InsertDetourOnSegment_uses_click_side_for_horizontal_segments()
    {
        var up = FactoryMapOrthogonalPathService.InsertDetourOnSegment(
            new Point(0, 50),
            [],
            new Point(100, 50),
            0,
            new Point(45, 35),
            10);

        AssertPoint(up[0], 50, 50);
        AssertPoint(up[1], 50, 40);
        AssertPoint(up[2], 100, 40);
        AssertOrthogonal(CreateFullPath(new Point(0, 50), up, new Point(100, 50)));

        var down = FactoryMapOrthogonalPathService.InsertDetourOnSegment(
            new Point(0, 50),
            [],
            new Point(100, 50),
            0,
            new Point(45, 65),
            10);

        AssertPoint(down[0], 50, 50);
        AssertPoint(down[1], 50, 70);
        AssertPoint(down[2], 100, 70);
        AssertOrthogonal(CreateFullPath(new Point(0, 50), down, new Point(100, 50)));
    }

    [Fact]
    public void InsertDetourOnSegment_uses_click_side_for_vertical_segments()
    {
        var left = FactoryMapOrthogonalPathService.InsertDetourOnSegment(
            new Point(50, 0),
            [],
            new Point(50, 100),
            0,
            new Point(35, 45),
            10);

        AssertPoint(left[0], 50, 50);
        AssertPoint(left[1], 40, 50);
        AssertPoint(left[2], 40, 100);
        AssertOrthogonal(CreateFullPath(new Point(50, 0), left, new Point(50, 100)));

        var right = FactoryMapOrthogonalPathService.InsertDetourOnSegment(
            new Point(50, 0),
            [],
            new Point(50, 100),
            0,
            new Point(65, 45),
            10);

        AssertPoint(right[0], 50, 50);
        AssertPoint(right[1], 70, 50);
        AssertPoint(right[2], 70, 100);
        AssertOrthogonal(CreateFullPath(new Point(50, 0), right, new Point(50, 100)));
    }

    [Fact]
    public void InsertDetourOnSegment_returns_normalized_points_for_invalid_segment_index()
    {
        var result = FactoryMapOrthogonalPathService.InsertDetourOnSegment(
            new Point(0, 0),
            [new FactoryMapPoint { X = 50, Y = 0 }],
            new Point(100, 0),
            99,
            new Point(50, 40),
            10);

        Assert.Empty(result);
    }

    [Fact]
    public void MoveSegment_moves_horizontal_segment_only_on_y_axis()
    {
        var result = FactoryMapOrthogonalPathService.MoveSegment(
            new Point(0, 0),
            [
                new FactoryMapPoint { X = 40, Y = 0 },
                new FactoryMapPoint { X = 40, Y = 30 },
                new FactoryMapPoint { X = 80, Y = 30 }
            ],
            new Point(80, 60),
            2,
            new Point(10, 55),
            10,
            snapToGrid: false);

        AssertPoint(result[1], 40, 55);
        AssertPoint(result[2], 80, 55);
        AssertOrthogonal(CreateFullPath(new Point(0, 0), result, new Point(80, 60)));
    }

    [Fact]
    public void MoveSegment_moves_vertical_segment_only_on_x_axis()
    {
        var result = FactoryMapOrthogonalPathService.MoveSegment(
            new Point(0, 0),
            [
                new FactoryMapPoint { X = 40, Y = 0 },
                new FactoryMapPoint { X = 40, Y = 30 },
                new FactoryMapPoint { X = 80, Y = 30 }
            ],
            new Point(80, 60),
            1,
            new Point(65, 99),
            10,
            snapToGrid: false);

        AssertPoint(result[0], 65, 0);
        AssertPoint(result[1], 65, 30);
        AssertOrthogonal(CreateFullPath(new Point(0, 0), result, new Point(80, 60)));
    }

    [Fact]
    public void MoveSegment_snaps_moved_axis_when_requested()
    {
        var result = FactoryMapOrthogonalPathService.MoveSegment(
            new Point(0, 0),
            [
                new FactoryMapPoint { X = 40, Y = 0 },
                new FactoryMapPoint { X = 40, Y = 30 },
                new FactoryMapPoint { X = 80, Y = 30 }
            ],
            new Point(80, 60),
            1,
            new Point(67, 99),
            10,
            snapToGrid: true);

        AssertPoint(result[0], 70, 0);
        AssertPoint(result[1], 70, 30);
        AssertOrthogonal(CreateFullPath(new Point(0, 0), result, new Point(80, 60)));
    }

    [Fact]
    public void MovePoint_keeps_all_segments_orthogonal_while_dragging()
    {
        var result = FactoryMapOrthogonalPathService.MovePoint(
            new Point(0, 0),
            [
                new FactoryMapPoint { X = 50, Y = 0 },
                new FactoryMapPoint { X = 50, Y = 40 }
            ],
            new Point(100, 40),
            0,
            new Point(70, 25),
            10,
            snapToGrid: false);

        AssertOrthogonal(CreateFullPath(new Point(0, 0), result, new Point(100, 40)));
    }

    [Fact]
    public void MovePoint_snaps_result_when_requested()
    {
        var result = FactoryMapOrthogonalPathService.MovePoint(
            new Point(0, 0),
            [
                new FactoryMapPoint { X = 50, Y = 0 },
                new FactoryMapPoint { X = 50, Y = 40 }
            ],
            new Point(100, 40),
            0,
            new Point(67, 25),
            10,
            snapToGrid: true);

        Assert.All(result, point =>
        {
            Assert.Equal(0, point.X % 10);
            Assert.Equal(0, point.Y % 10);
        });
        AssertOrthogonal(CreateFullPath(new Point(0, 0), result, new Point(100, 40)));
    }

    private static List<Point> CreateFullPath(Point start, IReadOnlyList<FactoryMapPoint> points, Point end)
    {
        var path = new List<Point> { start };
        path.AddRange(points.Select(point => new Point(point.X, point.Y)));
        path.Add(end);
        return path;
    }

    private static void AssertOrthogonal(IReadOnlyList<Point> path)
    {
        for (var i = 0; i < path.Count - 1; i++)
        {
            var first = path[i];
            var second = path[i + 1];
            Assert.True(
                Math.Abs(first.X - second.X) < 0.001 || Math.Abs(first.Y - second.Y) < 0.001,
                $"Segment {i} is diagonal: ({first.X},{first.Y}) -> ({second.X},{second.Y})");
        }
    }

    private static void AssertPoint(FactoryMapPoint point, double expectedX, double expectedY)
    {
        Assert.Equal(expectedX, point.X);
        Assert.Equal(expectedY, point.Y);
    }
}
