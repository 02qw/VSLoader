using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapTopologyServiceTests
{
    private readonly FactoryMapTopologyService service = new();

    [Fact]
    public void ConnectPoints_creates_orthogonal_bend_chain_and_rejects_duplicate_without_mutation()
    {
        var map = CreateMap();

        var first = service.ConnectPoints(map, "node-a:right", "free-1");

        Assert.True(first.Success, first.ErrorMessage);
        var bend = Assert.Single(map.ConnectionPoints, point => point.Kind == FactoryMapConnectionPointKinds.Bend);
        Assert.Equal(2, map.Segments.Count);
        Assert.All(map.Segments, segment => Assert.True(IsOrthogonal(map, segment)));
        Assert.Contains(map.Segments, segment => segment.FromPointId == "node-a:right" || segment.ToPointId == "node-a:right");
        Assert.Contains(map.Segments, segment => segment.FromPointId == "free-1" || segment.ToPointId == "free-1");
        Assert.Equal(2, service.GetDegree(map, bend.Id));

        var pointCount = map.ConnectionPoints.Count;
        var segmentCount = map.Segments.Count;
        var duplicate = service.ConnectPoints(map, "node-a:right", "free-1");

        Assert.False(duplicate.Success);
        Assert.Equal(pointCount, map.ConnectionPoints.Count);
        Assert.Equal(segmentCount, map.Segments.Count);
    }

    [Fact]
    public void ConnectPoints_rejects_self_missing_and_bend_branch_without_mutation()
    {
        var map = CreateMap();
        map.ConnectionPoints.Add(new FactoryMapConnectionPoint
        {
            Id = "bend-1",
            Kind = FactoryMapConnectionPointKinds.Bend,
            X = 300,
            Y = 129
        });
        map.Segments.AddRange(
        [
            new FactoryMapSegment { Id = "s1", FromPointId = "node-a:right", ToPointId = "bend-1" },
            new FactoryMapSegment { Id = "s2", FromPointId = "bend-1", ToPointId = "free-1" }
        ]);
        var snapshot = Snapshot(map);

        Assert.False(service.ConnectPoints(map, "free-1", "free-1").Success);
        Assert.False(service.ConnectPoints(map, "free-1", "missing").Success);
        Assert.False(service.ConnectPoints(map, "bend-1", "free-2").Success);
        Assert.Equal(snapshot, Snapshot(map));
    }

    [Fact]
    public void ConnectPoints_routes_top_to_left_away_from_both_device_edges()
    {
        var map = new FactoryMapDeviceViewData
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

        var result = service.ConnectPoints(map, "bottom:top", "right:left");

        Assert.True(result.Success, result.ErrorMessage);
        var sourceSegment = Assert.Single(map.Segments, segment => References(segment, "bottom:top"));
        var sourceNeighbor = GetOtherPoint(map, sourceSegment, "bottom:top");
        Assert.Equal(175, sourceNeighbor.X);
        Assert.True(sourceNeighbor.Y < 300);
        var targetSegment = Assert.Single(map.Segments, segment => References(segment, "right:left"));
        var targetNeighbor = GetOtherPoint(map, targetSegment, "right:left");
        Assert.Equal(129, targetNeighbor.Y);
        Assert.True(targetNeighbor.X < 400);
        Assert.All(map.Segments, segment => Assert.True(IsOrthogonal(map, segment)));
    }

    [Fact]
    public void SplitSegmentAt_creates_one_free_point_and_two_segments_atomically()
    {
        var map = CreateMap();
        map.ConnectionPoints.Single(point => point.Id == "free-1").Y = 129;
        map.Segments.Add(new FactoryMapSegment
        {
            Id = "segment-1",
            FromPointId = "node-a:right",
            ToPointId = "free-1",
            ZIndex = 7
        });

        var result = service.SplitSegmentAt(map, "segment-1", 310, 134, 10, 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(result.ReusedEndpoint);
        var inserted = Assert.Single(map.ConnectionPoints, point => point.Id == result.PointId);
        Assert.Equal(FactoryMapConnectionPointKinds.Free, inserted.Kind);
        Assert.Equal(310, inserted.X);
        Assert.Equal(129, inserted.Y);
        Assert.Equal(2, map.Segments.Count);
        Assert.DoesNotContain(map.Segments, segment => segment.Id == "segment-1");
        Assert.All(map.Segments, segment => Assert.Equal(7, segment.ZIndex));
    }

    [Fact]
    public void SplitSegmentAt_reuses_near_endpoint_without_changing_topology()
    {
        var map = CreateMap();
        map.ConnectionPoints.Single(point => point.Id == "free-1").Y = 129;
        map.Segments.Add(new FactoryMapSegment
        {
            Id = "segment-1",
            FromPointId = "node-a:right",
            ToPointId = "free-1"
        });
        var snapshot = Snapshot(map);

        var result = service.SplitSegmentAt(map, "segment-1", 254, 129, 10, 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.ReusedEndpoint);
        Assert.Equal("node-a:right", result.PointId);
        Assert.Equal(snapshot, Snapshot(map));
    }

    [Theory]
    [InlineData(310, 134, "horizontal")]
    [InlineData(255, 200, "vertical")]
    public void SplitSegmentWithJunctionAt_creates_axis_constrained_junction(
        double clickX,
        double clickY,
        string expectedAxis)
    {
        var map = CreateMap();
        FactoryMapSegment segment;
        if (expectedAxis == FactoryMapJunctionAxes.Horizontal)
        {
            map.ConnectionPoints.Single(point => point.Id == "free-1").Y = 129;
            segment = new FactoryMapSegment
            {
                Id = "segment-1",
                FromPointId = "node-a:right",
                ToPointId = "free-1"
            };
        }
        else
        {
            map.ConnectionPoints.Single(point => point.Id == "free-1").X = 250;
            segment = new FactoryMapSegment
            {
                Id = "segment-1",
                FromPointId = "node-a:right",
                ToPointId = "free-1"
            };
        }

        map.Segments.Add(segment);

        var result = service.SplitSegmentWithJunctionAt(
            map,
            segment.Id,
            clickX,
            clickY,
            gridSize: 10,
            endpointThreshold: 10);

        Assert.True(result.Success, result.ErrorMessage);
        var junction = map.ConnectionPoints.Single(point => point.Id == result.PointId);
        Assert.Equal(FactoryMapConnectionPointKinds.Junction, junction.Kind);
        Assert.Equal(expectedAxis, junction.JunctionAxis);
        Assert.Equal(2, map.Segments.Count);
    }

    [Fact]
    public void DeleteConnectionPoint_rejects_attached_and_removes_free_with_incident_segments()
    {
        var map = CreateMap();
        map.ConnectionPoints.Single(point => point.Id == "free-1").Y = 129;
        map.Segments.Add(new FactoryMapSegment
        {
            Id = "segment-1",
            FromPointId = "node-a:right",
            ToPointId = "free-1"
        });

        Assert.False(service.DeleteConnectionPoint(map, "node-a:right").Success);
        Assert.Contains(map.ConnectionPoints, point => point.Id == "node-a:right");

        var deleted = service.DeleteConnectionPoint(map, "free-1");

        Assert.True(deleted.Success, deleted.ErrorMessage);
        Assert.DoesNotContain(map.ConnectionPoints, point => point.Id == "free-1");
        Assert.Empty(map.Segments);
    }

    [Fact]
    public void CleanupOrphanBendPoints_never_removes_free_points()
    {
        var map = CreateMap();
        map.ConnectionPoints.Add(new FactoryMapConnectionPoint
        {
            Id = "bend-orphan",
            Kind = FactoryMapConnectionPointKinds.Bend,
            X = 420,
            Y = 300
        });
        map.ConnectionPoints.Add(new FactoryMapConnectionPoint
        {
            Id = "free-orphan",
            Kind = FactoryMapConnectionPointKinds.Free,
            X = 440,
            Y = 300
        });

        var removed = service.CleanupOrphanBendPoints(map);

        Assert.Equal(1, removed);
        Assert.DoesNotContain(map.ConnectionPoints, point => point.Id == "bend-orphan");
        Assert.Contains(map.ConnectionPoints, point => point.Id == "free-orphan");
    }

    [Fact]
    public void NormalizeJunctions_merges_collinear_degree_two_junction()
    {
        var map = CreateMap();
        map.ConnectionPoints.Add(new FactoryMapConnectionPoint
        {
            Id = "junction",
            Kind = FactoryMapConnectionPointKinds.Junction,
            JunctionAxis = FactoryMapJunctionAxes.Horizontal,
            X = 350,
            Y = 129
        });
        map.ConnectionPoints.Single(point => point.Id == "free-1").Y = 129;
        map.Segments.AddRange(
        [
            new FactoryMapSegment { Id = "left", FromPointId = "node-a:right", ToPointId = "junction" },
            new FactoryMapSegment { Id = "right", FromPointId = "junction", ToPointId = "free-1" }
        ]);

        var changed = service.NormalizeJunctions(map);

        Assert.Equal(1, changed);
        Assert.DoesNotContain(map.ConnectionPoints, point => point.Id == "junction");
        var merged = Assert.Single(map.Segments);
        Assert.True(References(merged, "node-a:right") && References(merged, "free-1"));
    }

    [Fact]
    public void NormalizeJunctions_converts_corner_and_single_junctions()
    {
        var map = CreateMap();
        map.ConnectionPoints.AddRange(
        [
            new FactoryMapConnectionPoint { Id = "corner", Kind = FactoryMapConnectionPointKinds.Junction, JunctionAxis = FactoryMapJunctionAxes.Horizontal, X = 350, Y = 129 },
            new FactoryMapConnectionPoint { Id = "single", Kind = FactoryMapConnectionPointKinds.Junction, JunctionAxis = FactoryMapJunctionAxes.Vertical, X = 600, Y = 200 }
        ]);
        map.ConnectionPoints.Single(point => point.Id == "free-1").X = 350;
        map.ConnectionPoints.Single(point => point.Id == "free-2").X = 600;
        map.Segments.AddRange(
        [
            new FactoryMapSegment { Id = "corner-a", FromPointId = "node-a:right", ToPointId = "corner" },
            new FactoryMapSegment { Id = "corner-b", FromPointId = "corner", ToPointId = "free-1" },
            new FactoryMapSegment { Id = "single-a", FromPointId = "single", ToPointId = "free-2" }
        ]);

        var changed = service.NormalizeJunctions(map);

        Assert.Equal(2, changed);
        Assert.Equal(FactoryMapConnectionPointKinds.Bend, map.ConnectionPoints.Single(point => point.Id == "corner").Kind);
        Assert.Equal(FactoryMapConnectionPointKinds.Free, map.ConnectionPoints.Single(point => point.Id == "single").Kind);
        Assert.All(map.ConnectionPoints.Where(point => point.Id is "corner" or "single"), point => Assert.Equal(string.Empty, point.JunctionAxis));
    }

    [Fact]
    public void DisconnectSegment_normalizes_degree_two_junction_after_branch_removal()
    {
        var map = CreateMap();
        map.ConnectionPoints.Single(point => point.Id == "free-1").Y = 129;
        map.ConnectionPoints.Add(new FactoryMapConnectionPoint
        {
            Id = "junction",
            Kind = FactoryMapConnectionPointKinds.Junction,
            JunctionAxis = FactoryMapJunctionAxes.Horizontal,
            X = 350,
            Y = 129
        });
        map.Segments.AddRange(
        [
            new FactoryMapSegment { Id = "left", FromPointId = "node-a:right", ToPointId = "junction" },
            new FactoryMapSegment { Id = "right", FromPointId = "junction", ToPointId = "free-1" },
            new FactoryMapSegment { Id = "branch", FromPointId = "junction", ToPointId = "free-2" }
        ]);

        var result = service.DisconnectSegment(map, "branch");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.DoesNotContain(map.ConnectionPoints, point => point.Id == "junction");
        var merged = Assert.Single(map.Segments);
        Assert.True(References(merged, "node-a:right") && References(merged, "free-1"));
    }

    [Fact]
    public void ConvertJunctionToFree_clears_axis_without_changing_segments()
    {
        var map = CreateMap();
        map.ConnectionPoints.Add(new FactoryMapConnectionPoint
        {
            Id = "junction",
            Kind = FactoryMapConnectionPointKinds.Junction,
            JunctionAxis = FactoryMapJunctionAxes.Vertical,
            X = 500,
            Y = 220
        });
        map.Segments.Add(new FactoryMapSegment
        {
            Id = "branch",
            FromPointId = "junction",
            ToPointId = "free-2"
        });

        var result = service.ConvertJunctionToFree(map, "junction");

        Assert.True(result.Success, result.ErrorMessage);
        var point = map.ConnectionPoints.Single(candidate => candidate.Id == "junction");
        Assert.Equal(FactoryMapConnectionPointKinds.Free, point.Kind);
        Assert.Equal(string.Empty, point.JunctionAxis);
        Assert.Single(map.Segments);
    }

    [Fact]
    public void Delete_free_point_recursively_removes_dangling_bend_chain()
    {
        var map = CreateMap();
        map.ConnectionPoints.Single(point => point.Id == "free-1").Y = 220;
        map.ConnectionPoints.Add(new FactoryMapConnectionPoint
        {
            Id = "bend-1",
            Kind = FactoryMapConnectionPointKinds.Bend,
            X = 250,
            Y = 220
        });
        map.Segments.AddRange(
        [
            new FactoryMapSegment { Id = "s1", FromPointId = "node-a:right", ToPointId = "bend-1" },
            new FactoryMapSegment { Id = "s2", FromPointId = "bend-1", ToPointId = "free-1" }
        ]);

        var result = service.DeleteConnectionPoint(map, "free-1");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.DoesNotContain(map.ConnectionPoints, point => point.Id == "bend-1");
        Assert.Empty(map.Segments);
        Assert.Contains(map.ConnectionPoints, point => point.Id == "node-a:right");
    }

    [Fact]
    public void ValidateTopology_reports_dangling_diagonal_zero_length_duplicate_and_bend_degree()
    {
        var map = CreateMap();
        map.ConnectionPoints.Add(new FactoryMapConnectionPoint { Id = "bend-1", Kind = FactoryMapConnectionPointKinds.Bend, X = 300, Y = 129 });
        map.ConnectionPoints.Add(new FactoryMapConnectionPoint { Id = "same", Kind = FactoryMapConnectionPointKinds.Free, X = 300, Y = 129 });
        map.Segments.AddRange(
        [
            new FactoryMapSegment { Id = "valid", FromPointId = "node-a:right", ToPointId = "bend-1" },
            new FactoryMapSegment { Id = "duplicate", FromPointId = "bend-1", ToPointId = "node-a:right" },
            new FactoryMapSegment { Id = "zero", FromPointId = "bend-1", ToPointId = "same" },
            new FactoryMapSegment { Id = "diagonal", FromPointId = "node-a:top", ToPointId = "free-1" },
            new FactoryMapSegment { Id = "missing", FromPointId = "bend-1", ToPointId = "missing-point" },
            new FactoryMapSegment { Id = "degree", FromPointId = "bend-1", ToPointId = "free-2" }
        ]);

        var result = service.ValidateTopology(map);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("重复", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("零长度", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("正交", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("不存在", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("折弯点", StringComparison.Ordinal));
    }

    private static FactoryMapDeviceViewData CreateMap()
    {
        var map = new FactoryMapDeviceViewData
        {
            Devices =
            [
                new FactoryMapDeviceViewNode { Id = "node-a", Key = "A", Name = "设备A", X = 100, Y = 100 }
            ],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "node-a:top", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-a", Side = "top", X = 175, Y = 100 },
                new FactoryMapConnectionPoint { Id = "node-a:right", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-a", Side = "right", X = 250, Y = 129 },
                new FactoryMapConnectionPoint { Id = "node-a:bottom", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-a", Side = "bottom", X = 175, Y = 158 },
                new FactoryMapConnectionPoint { Id = "node-a:left", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-a", Side = "left", X = 100, Y = 129 },
                new FactoryMapConnectionPoint { Id = "free-1", Kind = FactoryMapConnectionPointKinds.Free, X = 400, Y = 220 },
                new FactoryMapConnectionPoint { Id = "free-2", Kind = FactoryMapConnectionPointKinds.Free, X = 500, Y = 129 }
            ]
        };
        return map;
    }

    private static bool IsOrthogonal(FactoryMapDeviceViewData map, FactoryMapSegment segment)
    {
        var from = map.ConnectionPoints.Single(point => point.Id == segment.FromPointId);
        var to = map.ConnectionPoints.Single(point => point.Id == segment.ToPointId);
        return from.X == to.X || from.Y == to.Y;
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

    private static string Snapshot(FactoryMapDeviceViewData map)
    {
        return string.Join("|", map.ConnectionPoints
            .OrderBy(point => point.Id)
            .Select(point => $"P:{point.Id}:{point.Kind}:{point.X}:{point.Y}"))
            + "#"
            + string.Join("|", map.Segments
                .OrderBy(segment => segment.Id)
                .Select(segment => $"S:{segment.Id}:{segment.FromPointId}:{segment.ToPointId}:{segment.ZIndex}"));
    }
}
