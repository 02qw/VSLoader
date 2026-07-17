using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapMovementServiceTests
{
    private readonly FactoryMapMovementService service = new();

    [Fact]
    public void Keyboard_move_policy_keeps_device_movement_on_the_map_grid()
    {
        Assert.Equal(10, FactoryMapMovementService.GetKeyboardStep(
            shift: true,
            control: false,
            requireGridAlignment: true));
        Assert.True(FactoryMapMovementService.ShouldSnapKeyboardMovement(
            shift: true,
            requireGridAlignment: true));

        Assert.Equal(1, FactoryMapMovementService.GetKeyboardStep(
            shift: true,
            control: false,
            requireGridAlignment: false));
        Assert.False(FactoryMapMovementService.ShouldSnapKeyboardMovement(
            shift: true,
            requireGridAlignment: false));
    }

    [Fact]
    public void Move_device_updates_attached_points_and_applies_keyboard_steps()
    {
        var map = CreateMap();

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Device, "node-a"),
            13,
            7,
            snapToGrid: true,
            gridSize: 10);

        Assert.True(result.Success, result.ErrorMessage);
        var node = Assert.Single(map.Devices);
        Assert.Equal(110, node.X);
        Assert.Equal(110, node.Y);
        var right = map.ConnectionPoints.Single(point => point.Id == "node-a:right");
        Assert.Equal(270, right.X);
        Assert.Equal(140, right.Y);
    }

    [Fact]
    public void Move_attached_point_moves_owner_node_instead_of_detaching_point()
    {
        var map = CreateMap();

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "node-a:top"),
            10,
            0,
            snapToGrid: true,
            gridSize: 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(110, map.Devices.Single().X);
        Assert.Equal("node-a", map.ConnectionPoints.Single(point => point.Id == "node-a:top").OwnerNodeId);
    }

    [Fact]
    public void Move_free_point_supports_fine_step_without_grid_snap()
    {
        var map = CreateMap();

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "free-1"),
            1,
            -1,
            snapToGrid: false,
            gridSize: 10);

        Assert.True(result.Success, result.ErrorMessage);
        var point = map.ConnectionPoints.Single(item => item.Id == "free-1");
        Assert.Equal(401, point.X);
        Assert.Equal(219, point.Y);
    }

    [Fact]
    public void Move_free_point_reroutes_attached_chain_without_restoring_edge_hugging()
    {
        var map = new FactoryMapDeviceViewData
        {
            Devices = [new FactoryMapDeviceViewNode { Id = "node", X = 100, Y = 100 }],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "node:top", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node", Side = FactoryMapPortKinds.Top, X = 175, Y = 100 },
                new FactoryMapConnectionPoint { Id = "bad-bend", Kind = FactoryMapConnectionPointKinds.Bend, X = 400, Y = 100 },
                new FactoryMapConnectionPoint { Id = "free", Kind = FactoryMapConnectionPointKinds.Free, X = 400, Y = 50 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "bad-1", FromPointId = "node:top", ToPointId = "bad-bend" },
                new FactoryMapSegment { Id = "bad-2", FromPointId = "bad-bend", ToPointId = "free" }
            ]
        };

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "free"),
            10,
            10,
            snapToGrid: true,
            gridSize: 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal((410d, 60d), GetPoint(map, "free"));
        var sourceSegment = Assert.Single(map.Segments, segment => References(segment, "node:top"));
        var sourceNeighbor = GetOtherPoint(map, sourceSegment, "node:top");
        Assert.Equal(175, sourceNeighbor.X);
        Assert.True(sourceNeighbor.Y < 100);
    }

    [Fact]
    public void Move_horizontal_segment_moves_bend_channel_only_on_vertical_axis()
    {
        var map = CreateMap();
        map.ConnectionPoints.AddRange(
        [
            new FactoryMapConnectionPoint { Id = "bend-a", Kind = FactoryMapConnectionPointKinds.Bend, X = 260, Y = 200 },
            new FactoryMapConnectionPoint { Id = "bend-b", Kind = FactoryMapConnectionPointKinds.Bend, X = 360, Y = 200 }
        ]);
        map.Segments.Add(new FactoryMapSegment { Id = "channel", FromPointId = "bend-a", ToPointId = "bend-b" });

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "channel"),
            50,
            23,
            snapToGrid: true,
            gridSize: 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(220, map.ConnectionPoints.Single(point => point.Id == "bend-a").Y);
        Assert.Equal(220, map.ConnectionPoints.Single(point => point.Id == "bend-b").Y);
        Assert.Equal(260, map.ConnectionPoints.Single(point => point.Id == "bend-a").X);
        Assert.Equal(360, map.ConnectionPoints.Single(point => point.Id == "bend-b").X);
    }

    [Fact]
    public void Move_segment_preserves_selected_id_for_repeated_keyboard_movement()
    {
        var map = CreateMap();
        map.ConnectionPoints.AddRange(
        [
            new FactoryMapConnectionPoint { Id = "bend-a", Kind = FactoryMapConnectionPointKinds.Bend, X = 260, Y = 200 },
            new FactoryMapConnectionPoint { Id = "bend-b", Kind = FactoryMapConnectionPointKinds.Bend, X = 360, Y = 200 }
        ]);
        map.Segments.Add(new FactoryMapSegment
        {
            Id = "selected-channel",
            FromPointId = "bend-a",
            ToPointId = "bend-b"
        });

        var first = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "selected-channel"),
            0,
            10,
            snapToGrid: true,
            gridSize: 10);
        var second = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "selected-channel"),
            0,
            10,
            snapToGrid: true,
            gridSize: 10);

        Assert.True(first.Success, first.ErrorMessage);
        Assert.True(second.Success, second.ErrorMessage);
        Assert.Contains(map.Segments, segment => segment.Id == "selected-channel");
        Assert.Equal(220, map.ConnectionPoints.Single(point => point.Id == "bend-a").Y);
        Assert.Equal(220, map.ConnectionPoints.Single(point => point.Id == "bend-b").Y);
    }

    [Fact]
    public void Move_segment_preserves_selected_id_on_the_shifted_channel_between_fixed_endpoints()
    {
        var map = new FactoryMapDeviceViewData
        {
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "left", Kind = FactoryMapConnectionPointKinds.Free, X = 200, Y = 200 },
                new FactoryMapConnectionPoint { Id = "right", Kind = FactoryMapConnectionPointKinds.Free, X = 400, Y = 200 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "selected-channel", FromPointId = "left", ToPointId = "right" }
            ]
        };

        Assert.True(service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "selected-channel"),
            0,
            10,
            snapToGrid: true,
            gridSize: 10).Success);
        Assert.True(service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "selected-channel"),
            0,
            10,
            snapToGrid: true,
            gridSize: 10).Success);

        var selected = Assert.Single(map.Segments, segment => segment.Id == "selected-channel");
        var from = map.ConnectionPoints.Single(point => point.Id == selected.FromPointId);
        var to = map.ConnectionPoints.Single(point => point.Id == selected.ToPointId);
        Assert.Equal(220, from.Y);
        Assert.Equal(220, to.Y);
    }

    [Fact]
    public void Move_segment_preserves_selected_id_after_returning_to_original_axis()
    {
        var map = new FactoryMapDeviceViewData
        {
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "left", Kind = FactoryMapConnectionPointKinds.Free, X = 200, Y = 200 },
                new FactoryMapConnectionPoint { Id = "right", Kind = FactoryMapConnectionPointKinds.Free, X = 400, Y = 200 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "selected-channel", FromPointId = "left", ToPointId = "right" }
            ]
        };
        var selected = new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "selected-channel");

        Assert.True(service.MoveObject(map, selected, 0, 10, true, 10).Success);
        Assert.True(service.MoveObject(map, selected, 0, -10, true, 10).Success);
        var third = service.MoveObject(map, selected, 0, 10, true, 10);

        Assert.True(third.Success, third.ErrorMessage);
        Assert.Contains(map.Segments, segment => segment.Id == "selected-channel");
    }

    [Fact]
    public void Move_horizontal_segment_rejects_parallel_keyboard_movement()
    {
        var map = CreateMap();
        map.ConnectionPoints.AddRange(
        [
            new FactoryMapConnectionPoint { Id = "bend-a", Kind = FactoryMapConnectionPointKinds.Bend, X = 260, Y = 200 },
            new FactoryMapConnectionPoint { Id = "bend-b", Kind = FactoryMapConnectionPointKinds.Bend, X = 360, Y = 200 }
        ]);
        map.Segments.Add(new FactoryMapSegment { Id = "channel", FromPointId = "bend-a", ToPointId = "bend-b" });

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "channel"),
            10,
            0,
            snapToGrid: true,
            gridSize: 10);

        Assert.False(result.Success);
        Assert.Contains("上下", result.ErrorMessage);
        Assert.Equal(260, map.ConnectionPoints.Single(point => point.Id == "bend-a").X);
        Assert.Equal(360, map.ConnectionPoints.Single(point => point.Id == "bend-b").X);
        Assert.Equal(200, map.ConnectionPoints.Single(point => point.Id == "bend-a").Y);
    }

    [Fact]
    public void Move_horizontal_channel_rejects_movement_through_device()
    {
        var map = new FactoryMapDeviceViewData
        {
            Devices =
            [
                new FactoryMapDeviceViewNode { Id = "obstacle", X = 260, Y = 210 }
            ],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "left", Kind = FactoryMapConnectionPointKinds.Free, X = 200, Y = 200 },
                new FactoryMapConnectionPoint { Id = "right", Kind = FactoryMapConnectionPointKinds.Free, X = 500, Y = 200 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "selected-channel", FromPointId = "left", ToPointId = "right" }
            ]
        };

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "selected-channel"),
            0,
            20,
            snapToGrid: true,
            gridSize: 10);

        Assert.False(result.Success);
        Assert.Contains("节点", result.ErrorMessage);
        var segment = Assert.Single(map.Segments);
        Assert.Equal("selected-channel", segment.Id);
        Assert.Equal(200, map.ConnectionPoints.Single(point => point.Id == "left").Y);
        Assert.Equal(200, map.ConnectionPoints.Single(point => point.Id == "right").Y);
    }

    [Fact]
    public void Move_segment_rejects_detour_that_runs_along_attached_node_boundary()
    {
        var map = CreateMap();
        FactoryMapNodeGeometryService.SynchronizeAttachedPoints(map);
        map.ConnectionPoints.Single(point => point.Id == "free-1").Y = 130;
        map.Segments.Add(new FactoryMapSegment { Id = "fixed", FromPointId = "node-a:right", ToPointId = "free-1" });

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "fixed"),
            0,
            10,
            snapToGrid: true,
            gridSize: 10);

        Assert.False(result.Success);
        Assert.Contains("节点", result.ErrorMessage);
        Assert.Equal(130, map.ConnectionPoints.Single(point => point.Id == "node-a:right").Y);
        Assert.Equal(130, map.ConnectionPoints.Single(point => point.Id == "free-1").Y);
        Assert.Single(map.Segments);
    }

    [Fact]
    public void Repeated_node_moves_collapse_collinear_bends_without_removing_free_point()
    {
        var map = CreateMap();
        map.ConnectionPoints.Single(point => point.Id == "free-1").Y = 129;
        map.Segments.Add(new FactoryMapSegment { Id = "fixed", FromPointId = "node-a:right", ToPointId = "free-1" });

        Assert.True(service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Device, "node-a"),
            0,
            10,
            snapToGrid: true,
            gridSize: 10).Success);
        Assert.True(service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Device, "node-a"),
            0,
            10,
            snapToGrid: true,
            gridSize: 10).Success);

        Assert.Single(map.ConnectionPoints, point => point.Kind == FactoryMapConnectionPointKinds.Bend);
        Assert.Contains(map.ConnectionPoints, point => point.Id == "free-1" && point.Kind == FactoryMapConnectionPointKinds.Free);
        Assert.Equal(2, map.Segments.Count);
    }

    [Fact]
    public void Move_device_reroutes_incident_attached_chain_without_touching_unrelated_route()
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
                new FactoryMapConnectionPoint { Id = "right:left", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "right", Side = FactoryMapPortKinds.Left, X = 400, Y = 129 },
                new FactoryMapConnectionPoint { Id = "bad-bend", Kind = FactoryMapConnectionPointKinds.Bend, X = 400, Y = 300 },
                new FactoryMapConnectionPoint { Id = "free-a", Kind = FactoryMapConnectionPointKinds.Free, X = 600, Y = 300 },
                new FactoryMapConnectionPoint { Id = "free-b", Kind = FactoryMapConnectionPointKinds.Free, X = 700, Y = 300 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "bad-1", FromPointId = "bottom:top", ToPointId = "bad-bend" },
                new FactoryMapSegment { Id = "bad-2", FromPointId = "bad-bend", ToPointId = "right:left" },
                new FactoryMapSegment { Id = "unrelated", FromPointId = "free-a", ToPointId = "free-b", ZIndex = 9 }
            ]
        };

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Device, "bottom"),
            0,
            10,
            snapToGrid: true,
            gridSize: 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal((180d, 310d), GetPoint(map, "bottom:top"));
        var sourceSegment = Assert.Single(map.Segments, segment => References(segment, "bottom:top"));
        var sourceNeighbor = GetOtherPoint(map, sourceSegment, "bottom:top");
        Assert.Equal(180, sourceNeighbor.X);
        Assert.True(sourceNeighbor.Y < 310);
        Assert.Contains(map.Segments, segment => segment.Id == "unrelated" && segment.ZIndex == 9);
        Assert.Equal((600d, 300d), GetPoint(map, "free-a"));
        Assert.Equal((700d, 300d), GetPoint(map, "free-b"));
    }

    [Fact]
    public void MoveObjects_moves_connected_free_points_once_without_creating_bends()
    {
        var map = CreateMap();
        map.ConnectionPoints.Add(new FactoryMapConnectionPoint
        {
            Id = "free-2",
            Kind = FactoryMapConnectionPointKinds.Free,
            X = 500,
            Y = 220
        });
        map.Segments.Add(new FactoryMapSegment
        {
            Id = "free-link",
            FromPointId = "free-1",
            ToPointId = "free-2"
        });

        var result = service.MoveObjects(
            map,
            [
                new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "free-1"),
                new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "free-2")
            ],
            20,
            30,
            snapToGrid: true,
            gridSize: 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal((420d, 250d), GetPoint(map, "free-1"));
        Assert.Equal((520d, 250d), GetPoint(map, "free-2"));
        Assert.Single(map.Segments);
        Assert.DoesNotContain(map.ConnectionPoints, point => point.Kind == FactoryMapConnectionPointKinds.Bend);
    }

    [Fact]
    public void MoveObjects_moves_device_and_free_point_with_one_delta()
    {
        var map = CreateMap();

        var result = service.MoveObjects(
            map,
            [
                new FactoryMapObjectRef(FactoryMapObjectKind.Device, "node-a"),
                new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "free-1")
            ],
            10,
            20,
            snapToGrid: true,
            gridSize: 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(110, map.Devices.Single().X);
        Assert.Equal(120, map.Devices.Single().Y);
        Assert.Equal((410d, 240d), GetPoint(map, "free-1"));
        Assert.Equal((270d, 150d), GetPoint(map, "node-a:right"));
    }

    [Fact]
    public void MoveObjects_rejects_invalid_member_without_partial_mutation()
    {
        var map = CreateMap();

        var result = service.MoveObjects(
            map,
            [
                new FactoryMapObjectRef(FactoryMapObjectKind.Device, "node-a"),
                new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "missing")
            ],
            20,
            20,
            snapToGrid: true,
            gridSize: 10);

        Assert.False(result.Success);
        Assert.Equal(100, map.Devices.Single().X);
        Assert.Equal(100, map.Devices.Single().Y);
        Assert.Equal((400d, 220d), GetPoint(map, "free-1"));
    }

    [Fact]
    public void Horizontal_junction_moves_only_along_trunk_axis()
    {
        var map = CreateJunctionMap(FactoryMapJunctionAxes.Horizontal);

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "junction"),
            20,
            40,
            snapToGrid: true,
            gridSize: 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal((370d, 129d), GetPoint(map, "junction"));
        Assert.All(map.Segments, segment => Assert.True(IsOrthogonal(map, segment)));
    }

    [Fact]
    public void Vertical_junction_moves_only_along_trunk_axis()
    {
        var map = CreateJunctionMap(FactoryMapJunctionAxes.Vertical);

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "junction"),
            40,
            20,
            snapToGrid: true,
            gridSize: 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal((250d, 220d), GetPoint(map, "junction"));
        Assert.All(map.Segments, segment => Assert.True(IsOrthogonal(map, segment)));
    }

    [Fact]
    public void Locked_junction_rejects_direct_movement()
    {
        var map = CreateJunctionMap(FactoryMapJunctionAxes.Locked);

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "junction"),
            20,
            20,
            snapToGrid: true,
            gridSize: 10);

        Assert.False(result.Success);
        Assert.Equal((350d, 200d), GetPoint(map, "junction"));
    }

    [Fact]
    public void Moving_horizontal_trunk_channel_moves_junction_and_keeps_fixed_boundaries_orthogonal()
    {
        var map = CreateJunctionMap(FactoryMapJunctionAxes.Horizontal);

        var result = service.MoveObject(
            map,
            new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "left-trunk"),
            0,
            20,
            snapToGrid: true,
            gridSize: 10);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal((350d, 150d), GetPoint(map, "junction"));
        Assert.Equal((200d, 129d), GetPoint(map, "left"));
        Assert.Equal((500d, 129d), GetPoint(map, "right"));
        Assert.All(map.Segments, segment => Assert.True(IsOrthogonal(map, segment)));
    }

    private static FactoryMapDeviceViewData CreateJunctionMap(string axis)
    {
        if (axis == FactoryMapJunctionAxes.Vertical)
        {
            return new FactoryMapDeviceViewData
            {
                ConnectionPoints =
                [
                    new FactoryMapConnectionPoint { Id = "top", Kind = FactoryMapConnectionPointKinds.Free, X = 250, Y = 100 },
                    new FactoryMapConnectionPoint { Id = "junction", Kind = FactoryMapConnectionPointKinds.Junction, JunctionAxis = axis, X = 250, Y = 200 },
                    new FactoryMapConnectionPoint { Id = "bottom", Kind = FactoryMapConnectionPointKinds.Free, X = 250, Y = 300 }
                ],
                Segments =
                [
                    new FactoryMapSegment { Id = "top-trunk", FromPointId = "top", ToPointId = "junction" },
                    new FactoryMapSegment { Id = "bottom-trunk", FromPointId = "junction", ToPointId = "bottom" }
                ]
            };
        }

        return new FactoryMapDeviceViewData
        {
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "left", Kind = FactoryMapConnectionPointKinds.Free, X = 200, Y = 129 },
                new FactoryMapConnectionPoint { Id = "junction", Kind = FactoryMapConnectionPointKinds.Junction, JunctionAxis = axis, X = 350, Y = axis == FactoryMapJunctionAxes.Locked ? 200 : 129 },
                new FactoryMapConnectionPoint { Id = "right", Kind = FactoryMapConnectionPointKinds.Free, X = 500, Y = 129 }
            ],
            Segments = axis == FactoryMapJunctionAxes.Locked
                ? []
                :
                [
                    new FactoryMapSegment { Id = "left-trunk", FromPointId = "left", ToPointId = "junction" },
                    new FactoryMapSegment { Id = "right-trunk", FromPointId = "junction", ToPointId = "right" }
                ]
        };
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

    private static (double X, double Y) GetPoint(FactoryMapDeviceViewData map, string pointId)
    {
        var point = map.ConnectionPoints.Single(candidate => candidate.Id == pointId);
        return (point.X, point.Y);
    }

    private static FactoryMapDeviceViewData CreateMap()
    {
        return new FactoryMapDeviceViewData
        {
            Devices = [new FactoryMapDeviceViewNode { Id = "node-a", Key = "A", Name = "设备A", X = 100, Y = 100 }],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "node-a:top", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-a", Side = "top", X = 175, Y = 100 },
                new FactoryMapConnectionPoint { Id = "node-a:right", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-a", Side = "right", X = 250, Y = 129 },
                new FactoryMapConnectionPoint { Id = "node-a:bottom", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-a", Side = "bottom", X = 175, Y = 158 },
                new FactoryMapConnectionPoint { Id = "node-a:left", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-a", Side = "left", X = 100, Y = 129 },
                new FactoryMapConnectionPoint { Id = "free-1", Kind = FactoryMapConnectionPointKinds.Free, X = 400, Y = 220 }
            ]
        };
    }
}
