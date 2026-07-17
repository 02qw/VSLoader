using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSLoader.Models;
using VSLoader.Views;

namespace VSLoader.Tests;

[Collection(WpfApplicationTestCollection.Name)]
public sealed class FactoryMapWindowRuntimeLoadTests
{
    [Fact]
    public void Visible_factory_map_edges_inset_endpoints_under_nodes_to_avoid_zoom_pan_artifacts()
    {
        var edge = new FactoryMapDeviceEdgeViewData
        {
            From = new FactoryMapDeviceViewNode { X = 100, Y = 200 },
            To = new FactoryMapDeviceViewNode { X = 360, Y = 220 }
        };

        var points = FactoryMapWindow.CreateVisibleEdgePoints(edge);

        Assert.Equal(4, points.Count);
        Assert.Equal(100 + FactoryMapWindow.DeviceNodeWidth - FactoryMapWindow.EdgeEndpointInset, points[0].X);
        Assert.Equal(360 + FactoryMapWindow.EdgeEndpointInset, points[points.Count - 1].X);
    }

    [Fact]
    public void Visible_factory_map_edges_use_manual_points_between_inset_endpoints()
    {
        var edge = new FactoryMapDeviceEdgeViewData
        {
            From = new FactoryMapDeviceViewNode { X = 100, Y = 200 },
            To = new FactoryMapDeviceViewNode { X = 360, Y = 220 },
            Points =
            [
                new FactoryMapPoint { X = 260, Y = 229 },
                new FactoryMapPoint { X = 260, Y = 249 }
            ]
        };

        var points = FactoryMapWindow.CreateVisibleEdgePoints(edge);

        Assert.Equal(4, points.Count);
        Assert.Equal(100 + FactoryMapWindow.DeviceNodeWidth - FactoryMapWindow.EdgeEndpointInset, points[0].X);
        Assert.Equal(260, points[1].X);
        Assert.Equal(250, points[2].Y);
        Assert.Equal(360 + FactoryMapWindow.EdgeEndpointInset, points[3].X);
    }

    [Fact]
    public void Visible_factory_map_edges_use_configured_top_and_bottom_ports()
    {
        var edge = new FactoryMapDeviceEdgeViewData
        {
            From = new FactoryMapDeviceViewNode { X = 100, Y = 100 },
            FromPort = FactoryMapPortKinds.Bottom,
            To = new FactoryMapDeviceViewNode { X = 120, Y = 320 },
            ToPort = FactoryMapPortKinds.Top
        };

        var points = FactoryMapWindow.CreateVisibleEdgePoints(edge);

        Assert.Equal(4, points.Count);
        Assert.Equal(180, points[0].X);
        Assert.Equal(100 + FactoryMapWindow.DeviceNodeHeight - FactoryMapWindow.EdgeEndpointInset, points[0].Y);
        Assert.Equal(200, points[points.Count - 1].X);
        Assert.Equal(320 + FactoryMapWindow.EdgeEndpointInset, points[points.Count - 1].Y);
    }

    [Fact]
    public void Visible_factory_map_edges_normalize_manual_points_to_orthogonal_path()
    {
        var edge = new FactoryMapDeviceEdgeViewData
        {
            From = new FactoryMapDeviceViewNode { X = 100, Y = 200 },
            To = new FactoryMapDeviceViewNode { X = 360, Y = 220 },
            Points =
            [
                new FactoryMapPoint { X = 260, Y = 280 }
            ]
        };

        var points = FactoryMapWindow.CreateVisibleEdgePoints(edge);

        AssertOrthogonal(points);
    }

    [Fact]
    public void Visible_factory_map_edges_use_flat_line_caps_to_avoid_node_side_artifacts()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        var visiblePolylineStart = code.IndexOf("var polyline = new Polyline", StringComparison.Ordinal);
        var hitPolylineStart = code.IndexOf("var hitPolyline = new Polyline", StringComparison.Ordinal);
        Assert.True(visiblePolylineStart >= 0);
        Assert.True(hitPolylineStart > visiblePolylineStart);

        var visiblePolylineBlock = code[visiblePolylineStart..hitPolylineStart];
        Assert.Contains("StrokeStartLineCap = PenLineCap.Flat", visiblePolylineBlock);
        Assert.Contains("StrokeEndLineCap = PenLineCap.Flat", visiblePolylineBlock);
    }

    [Fact]
    public void Map_window_captures_preview_mouse_wheel_even_when_child_elements_handle_it()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml"));
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));
        var mainWindowCode = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.DoesNotContain("PreviewMouseWheel=\"MapViewport_MouseWheel\"", xaml);
        Assert.DoesNotContain("\n                  MouseWheel=\"MapViewport_MouseWheel\"", xaml);
        Assert.Contains("AddHandler(Mouse.PreviewMouseWheelEvent", code);
        Assert.Contains("handledEventsToo: true", code);
        Assert.Contains("FactoryMapWindow_PreviewMouseWheel", code);
        Assert.Contains("HwndSource.FromHwnd", code);
        Assert.Contains("WM_MOUSEWHEEL", code);
        Assert.Contains("WM_MOUSEHWHEEL", code);
        Assert.Contains("FactoryMapWindow_WndProc", code);
        Assert.Contains("WH_MOUSE_LL", code);
        Assert.Contains("SetWindowsHookEx", code);
        Assert.Contains("FocusVisualStyle=\"{x:Null}\"", xaml);
        Assert.Contains("public void RestoreMapInputFocus()", code);
        Assert.Contains("_factoryMapWindow.RestoreMapInputFocus();", mainWindowCode);
    }

    [Fact]
    public void Map_focus_restore_is_cancelable_generation_guarded_and_active_window_only()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("private long mapFocusRestoreGeneration;", code);
        Assert.Contains("public void CancelPendingInputFocusRestore()", code);
        Assert.Contains("var requestedGeneration = ++mapFocusRestoreGeneration;", code);
        Assert.Contains("requestedGeneration != mapFocusRestoreGeneration", code);
        Assert.Contains("!IsVisible || !IsActive", code);
    }

    [Fact]
    public void Map_window_interactions_do_not_write_synchronous_diagnostic_logs()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.DoesNotContain("factory-map.debug.log", code);
        Assert.DoesNotContain("RollingLogFileWriter", code);
        Assert.DoesNotContain("WriteMapDebugLog", code);
        Assert.DoesNotContain("WriteMapWheelDebugLog", code);
        Assert.DoesNotContain("BuildMapDebugSnapshot", code);
    }

    [Fact]
    public void Browse_mode_right_click_selects_the_target_device_before_opening_its_menu()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        var handlerStart = code.IndexOf(
            "private void Device_PreviewMouseRightButtonDown",
            StringComparison.Ordinal);
        var nextMethodStart = code.IndexOf(
            "private ContextMenu CreateTopologyDeviceContextMenu",
            handlerStart,
            StringComparison.Ordinal);

        Assert.True(handlerStart >= 0);
        Assert.True(nextMethodStart > handlerStart);

        var handler = code[handlerStart..nextMethodStart];
        var selectionIndex = handler.IndexOf("SelectBrowseDevice(device);", StringComparison.Ordinal);
        var menuCreationIndex = handler.IndexOf("CreateDeviceContextMenu(device)", StringComparison.Ordinal);

        Assert.True(selectionIndex >= 0);
        Assert.True(menuCreationIndex > selectionIndex);
    }

    [Fact]
    public void Modern_context_menus_suppress_right_button_input_before_menu_items_can_execute()
    {
        var mainWindow = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml"));
        var mapWindow = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));
        var workspaceWindow = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "WorkspaceSelectorWindow.xaml.cs"));
        var behaviorPath = TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Behaviors",
            "ContextMenuInputBehavior.cs");

        Assert.True(File.Exists(behaviorPath), "公共右键菜单输入行为必须存在。");
        var behavior = File.ReadAllText(behaviorPath);

        Assert.Contains(
            "behaviors:ContextMenuInputBehavior.SuppressRightClickActivation=\"True\"",
            mainWindow);
        Assert.Contains(
            "ContextMenuInputBehavior.SetSuppressRightClickActivation(menu, true);",
            mapWindow);
        Assert.Contains(
            "ContextMenuInputBehavior.SetSuppressRightClickActivation(menu, true);",
            workspaceWindow);
        Assert.Contains("menu.PreviewMouseRightButtonDown += SuppressRightClick;", behavior);
        Assert.Contains("menu.PreviewMouseRightButtonUp += SuppressRightClick;", behavior);
        Assert.Contains("e.Handled = true;", behavior);
    }

    [Fact]
    public void Visible_factory_map_edges_merge_overlapping_shared_segments()
    {
        var first = new FactoryMapDeviceEdgeViewData
        {
            From = FactoryMapEndpointViewData.FromConnector(new FactoryMapConnectorViewNode { X = 0, Y = 0 }),
            FromPort = FactoryMapPortKinds.Right,
            To = FactoryMapEndpointViewData.FromConnector(new FactoryMapConnectorViewNode { X = 100, Y = 0 }),
            ToPort = FactoryMapPortKinds.Left
        };
        var second = new FactoryMapDeviceEdgeViewData
        {
            From = FactoryMapEndpointViewData.FromConnector(new FactoryMapConnectorViewNode { X = 40, Y = 0 }),
            FromPort = FactoryMapPortKinds.Right,
            To = FactoryMapEndpointViewData.FromConnector(new FactoryMapConnectorViewNode { X = 160, Y = 0 }),
            ToPort = FactoryMapPortKinds.Left
        };

        var segments = FactoryMapWindow.CreateMergedVisibleEdgeSegments([first, second]);

        Assert.Single(segments);
        Assert.Equal(2, segments[0].Start.X);
        Assert.Equal(158, segments[0].End.X);
    }

    [Fact]
    public void Visible_factory_map_edges_split_non_overlapping_segments_on_same_axis()
    {
        var first = new FactoryMapDeviceEdgeViewData
        {
            From = FactoryMapEndpointViewData.FromConnector(new FactoryMapConnectorViewNode { X = 0, Y = 0 }),
            FromPort = FactoryMapPortKinds.Right,
            To = FactoryMapEndpointViewData.FromConnector(new FactoryMapConnectorViewNode { X = 100, Y = 0 }),
            ToPort = FactoryMapPortKinds.Left
        };
        var second = new FactoryMapDeviceEdgeViewData
        {
            From = FactoryMapEndpointViewData.FromConnector(new FactoryMapConnectorViewNode { X = 400, Y = 0 }),
            FromPort = FactoryMapPortKinds.Right,
            To = FactoryMapEndpointViewData.FromConnector(new FactoryMapConnectorViewNode { X = 520, Y = 0 }),
            ToPort = FactoryMapPortKinds.Left
        };

        var segments = FactoryMapWindow.CreateMergedVisibleEdgeSegments([first, second]);

        Assert.Equal(2, segments.Count);
    }

    [Fact]
    public void Edge_context_menu_offers_branch_point_without_exposing_edge_points()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        var addPointIndex = code.IndexOf("\"插入分支点\"", StringComparison.Ordinal);
        var deleteIndex = code.IndexOf("\"删除连线\"", StringComparison.Ordinal);

        Assert.True(addPointIndex >= 0);
        Assert.True(deleteIndex > addPointIndex);
        Assert.DoesNotContain("\"清除路径折点\"", code);
        Assert.DoesNotContain("clearItem.IsEnabled = HasManualEdgePoints(edge);", code);
    }

    [Fact]
    public void Legacy_connector_interactions_do_not_restore_a_separate_connect_mode()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("private void DrawConnector", code);
        Assert.Contains("connectorElement.MouseLeftButtonDown += Connector_MouseLeftButtonDown;", code);
        Assert.Contains("connectorElement.MouseMove += Connector_MouseMove;", code);
        Assert.Contains("connectorElement.MouseLeftButtonUp += Connector_MouseLeftButtonUp;", code);
        Assert.Contains("connectorElement.PreviewMouseRightButtonDown += Connector_PreviewMouseRightButtonDown;", code);
        Assert.Contains("private void AddConnectorOnEdge_Click", code);
        Assert.Contains("currentMap.Edges.Remove(edge);", code);
        Assert.Contains("删除分支点", code);
        Assert.DoesNotContain("private bool isConnectMode", code);
        Assert.DoesNotContain("UpdateConnectModeVisual", code);
    }

    [Fact]
    public void Unified_edit_mode_uses_topology_points_instead_of_node_body_guessing()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("private readonly FactoryMapConnectionDraftService connectionDraftService = new();", code);
        Assert.Contains("private void DrawTopologyPoint", code);
        Assert.Contains("private void TopologyPoint_PreviewMouseLeftButtonDown", code);
        Assert.Contains("HandleTopologyPointConnection", code);
        Assert.DoesNotContain("HandleDeviceClickInConnectMode(border, device);", code);
        Assert.DoesNotContain("HandleConnectorClickInConnectMode(element, connector);", code);
    }

    [Fact]
    public void Topology_snapshot_preserves_junction_axis_for_save_rollback()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        var captureStart = code.IndexOf("private static TopologySnapshot CaptureTopologySnapshot", StringComparison.Ordinal);
        var nextMethod = code.IndexOf("private ContextMenu CreateEdgeContextMenu", captureStart, StringComparison.Ordinal);
        Assert.True(captureStart >= 0);
        Assert.True(nextMethod > captureStart);
        Assert.Contains("JunctionAxis = point.JunctionAxis", code[captureStart..nextMethod]);
    }

    [Fact]
    public void Connection_draft_completes_point_and_segment_origins_atomically()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("private void CompleteConnectionAtTopologySegment", code);
        Assert.Contains("connectionDraftService.CompleteToSegment", code);
        Assert.Contains("connectionDraftService.CompleteToPoint", code);
        Assert.Contains("TrySaveTopologyChange(snapshot, \"连接到线段后保存失败。\")", code);
        Assert.Contains("interactionState.BeginSegmentConnectionDraft", code);
        Assert.Contains("从此处建立分支", code);
        Assert.DoesNotContain("插入普通连接点", code);
        Assert.DoesNotContain("private void InsertTopologyPointOnSegment", code);
    }

    [Fact]
    public void Junction_points_use_axis_cursor_diamond_visual_and_dedicated_menu_actions()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("FactoryMapConnectionPointKinds.Junction => 9d", code);
        Assert.Contains("new RotateTransform(45)", code);
        Assert.Contains("private static WpfCursor GetTopologyPointCursor", code);
        Assert.Contains("FactoryMapJunctionAxes.Horizontal => WpfCursors.SizeWE", code);
        Assert.Contains("FactoryMapJunctionAxes.Vertical => WpfCursors.SizeNS", code);
        Assert.Contains("FactoryMapJunctionAxes.Locked => WpfCursors.No", code);
        Assert.Contains("分支连接点", code);
        Assert.Contains("转换为普通连接点", code);
        Assert.Contains("topologyService.ConvertJunctionToFree", code);
        Assert.Contains("删除分支连接点", code);
    }

    [Fact]
    public void Segment_connection_draft_projects_to_grid_and_draws_nonpersistent_junction_preview()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("TryProjectTopologySegmentPoint", code);
        Assert.Contains("interactionState.BeginSegmentConnectionDraft(segmentId, projected.X, projected.Y)", code);
        Assert.Contains("private void DrawTopologyConnectionDraftPreview", code);
        Assert.Contains("DrawTopologyConnectionDraftPreview();", code);
        Assert.Contains("请选择分支连接终点", code);
        Assert.DoesNotContain("currentMap.ConnectionPoints.Add(preview", code);
    }

    [Fact]
    public void Map_toolbar_exposes_only_browse_and_edit_modes()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml"));
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("x:Name=\"BrowseModeButton\"", xaml);
        Assert.Contains("x:Name=\"EditModeButton\"", xaml);
        Assert.DoesNotContain("x:Name=\"ConnectModeButton\"", xaml);
        Assert.DoesNotContain("x:Name=\"MultiSelectModeButton\"", xaml);
        Assert.Contains("private void BrowseModeButton_Click", code);
        Assert.Contains("private void EditModeButton_Click", code);
        Assert.DoesNotContain("private void ConnectModeButton_Click", code);
        Assert.DoesNotContain("private void MultiSelectModeButton_Click", code);
    }

    [Fact]
    public void Map_toolbar_mode_buttons_restore_map_focus_and_use_central_visual_helpers()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("private void RestoreMapFocusAfterToolbarClick()", code);
        Assert.Contains("private bool IsMouseOverMapTitleBar()", code);
        Assert.Contains("FactoryMapSkipViewportFocusMouseOverTitleBar", code);
        Assert.Contains("private void UpdateMapModeVisual", code);
        Assert.Contains("private void RefreshMapModeStatus()", code);

        var browseModeStart = code.IndexOf("private void BrowseModeButton_Click", StringComparison.Ordinal);
        var editModeStart = code.IndexOf("private void EditModeButton_Click", StringComparison.Ordinal);
        var importStart = code.IndexOf("private void ImportMapButton_Click", StringComparison.Ordinal);

        Assert.True(browseModeStart >= 0);
        Assert.True(editModeStart > browseModeStart);
        Assert.True(importStart > editModeStart);

        var browseBlock = code[browseModeStart..editModeStart];
        var editBlock = code[editModeStart..importStart];

        Assert.Contains("RestoreMapFocusAfterToolbarClick();", browseBlock);
        Assert.Contains("RestoreMapFocusAfterToolbarClick();", editBlock);
        Assert.Contains("SetMapMode(FactoryMapMode.Browse)", browseBlock);
        Assert.Contains("SetMapMode(FactoryMapMode.Edit)", editBlock);
    }

    [Fact]
    public void Map_toolbar_exposes_arrange_lines_command_between_export_and_download()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml"));

        var exportIndex = xaml.IndexOf("x:Name=\"ExportMapButton\"", StringComparison.Ordinal);
        var arrangeIndex = xaml.IndexOf("x:Name=\"ArrangeLinesButton\"", StringComparison.Ordinal);
        var downloadIndex = xaml.IndexOf("x:Name=\"DownloadAdminUiLinksButton\"", StringComparison.Ordinal);

        Assert.True(exportIndex >= 0);
        Assert.True(arrangeIndex > exportIndex);
        Assert.True(downloadIndex > arrangeIndex);

        var arrangeBlock = xaml[arrangeIndex..downloadIndex];
        Assert.Contains("Style=\"{StaticResource ModernToolbarButtonStyle}\"", arrangeBlock);
        Assert.Contains("Content=\"整理线路\"", arrangeBlock);
        Assert.Contains("Click=\"ArrangeLinesButton_Click\"", arrangeBlock);
    }

    [Fact]
    public void Arrange_lines_command_runs_on_clone_with_busy_guard_and_save_rollback()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("private readonly FactoryMapLineArrangementService lineArrangementService = new();", code);
        Assert.Contains("private CancellationTokenSource? arrangeLinesCancellationTokenSource;", code);
        Assert.Contains("private long arrangeLinesOperationVersion;", code);
        Assert.Contains("private bool isArrangingLines;", code);

        var arrangeStart = code.IndexOf("private async void ArrangeLinesButton_Click", StringComparison.Ordinal);
        var nextMethod = code.IndexOf("private void DownloadAdminUiLinksButton_Click", arrangeStart, StringComparison.Ordinal);
        Assert.True(arrangeStart >= 0);
        Assert.True(nextMethod > arrangeStart);

        var arrangeBlock = code[arrangeStart..nextMethod];
        Assert.Contains("CanArrangeLines()", arrangeBlock);
        Assert.Contains("FlushPendingTopologySave()", arrangeBlock);
        Assert.Contains("确定要整理当前地图的全部线路吗？", arrangeBlock);
        Assert.Contains("CaptureTopologySnapshot(sourceMap)", arrangeBlock);
        Assert.Contains("CloneMapForLineArrangement(sourceMap)", arrangeBlock);
        Assert.Contains("BusyOverlayHost.Map", arrangeBlock);
        Assert.Contains("正在整理地图线路...", arrangeBlock);
        Assert.Contains("Task.Run", arrangeBlock);
        Assert.Contains("lineArrangementService.ArrangeAll", arrangeBlock);
        Assert.Contains("operationVersion != arrangeLinesOperationVersion", arrangeBlock);
        Assert.Contains("ReferenceEquals(currentMap, sourceMap)", arrangeBlock);
        Assert.Contains("saveLayout(currentMap)", arrangeBlock);
        Assert.Contains("RestoreTopologySnapshot(currentMap, snapshot)", arrangeBlock);

        Assert.Contains("private bool CanArrangeLines()", code);
        Assert.Contains("interactionState.Mode == FactoryMapMode.Edit", code);
        Assert.Contains("interactionState.Kind == FactoryMapInteractionKind.Idle", code);
        Assert.Contains("!HasTopologyConnectionDraft", code);
        Assert.Contains("!isArrangingLines", code);
        Assert.Contains("!IsMapBusy", code);
        Assert.Contains("ArrangeLinesButton.IsEnabled = CanArrangeLines();", code);
        Assert.Contains("arrangeLinesCancellationTokenSource?.Cancel();", code);
    }

    [Fact]
    public void Overlapping_segment_menu_headers_use_readable_route_names_instead_of_internal_ids()
    {
        var map = new FactoryMapDeviceViewData
        {
            TopologyAuthoritative = true,
            Devices =
            [
                new FactoryMapDeviceViewNode { Id = "node-a", Name = "设备甲" },
                new FactoryMapDeviceViewNode { Id = "node-b", Name = "设备乙" },
                new FactoryMapDeviceViewNode { Id = "node-c", Name = "设备丙" },
                new FactoryMapDeviceViewNode { Id = "node-d", Name = "设备丁" }
            ],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint { Id = "a:right", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-a" },
                new FactoryMapConnectionPoint { Id = "b:left", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-b" },
                new FactoryMapConnectionPoint { Id = "c:right", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-c" },
                new FactoryMapConnectionPoint { Id = "d:left", Kind = FactoryMapConnectionPointKinds.Attached, OwnerNodeId = "node-d" }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "segment-internal-a", FromPointId = "a:right", ToPointId = "b:left" },
                new FactoryMapSegment { Id = "segment-internal-b", FromPointId = "c:right", ToPointId = "d:left" }
            ]
        };
        var visibleSegment = new FactoryMapVisibleSegment
        {
            SourceSegmentIds = ["segment-internal-a", "segment-internal-b"],
            TopSegmentId = "segment-internal-a"
        };

        var headers = FactoryMapWindow.CreateTopologySegmentMenuHeaders(map, visibleSegment);

        Assert.Equal(
            ["线路 1：设备甲 → 设备乙（当前顶层）", "线路 2：设备丙 → 设备丁"],
            headers);
        Assert.DoesNotContain(headers, header => header.Contains("segment-internal", StringComparison.Ordinal));
    }

    [Fact]
    public void Overlapping_segment_menu_selection_targets_the_requested_underlying_segment()
    {
        var map = new FactoryMapDeviceViewData
        {
            Segments =
            [
                new FactoryMapSegment { Id = "top" },
                new FactoryMapSegment { Id = "underlying" }
            ]
        };
        var selection = new FactoryMapSelectionState();
        selection.Select(new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "top"));

        var selected = FactoryMapWindow.TrySelectTopologySegmentForEditing(map, selection, "underlying");
        var missing = FactoryMapWindow.TrySelectTopologySegmentForEditing(map, selection, "missing");

        Assert.True(selected);
        Assert.False(missing);
        Assert.Equal(
            new FactoryMapObjectRef(FactoryMapObjectKind.Segment, "underlying"),
            selection.PrimaryObject);
    }

    [Fact]
    public void Overlapping_segment_menu_headers_bind_direct_selection_and_restore_map_focus()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("group.AddHandler(", code);
        Assert.Contains("Mouse.PreviewMouseUpEvent", code);
        Assert.Contains("handledEventsToo: true", code);
        Assert.Contains("private void TopologySegmentMenuGroup_PreviewMouseLeftButtonUp", code);
        Assert.Contains("private void SelectTopologySegmentFromContextMenu", code);
        Assert.Contains("TrySelectTopologySegmentForEditing(currentMap, topologySelection, segmentId)", code);
        Assert.Contains("RenderCurrentMap(resetView: false);", code);
        Assert.Contains("RestoreMapFocusAfterToolbarClick();", code);
    }

    [Fact]
    public void Map_mode_status_badge_keeps_content_sized_capsule_layout()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml"));

        var badgeStart = xaml.IndexOf("x:Name=\"ModeStatusBadge\"", StringComparison.Ordinal);
        Assert.True(badgeStart >= 0);

        var badgeBlock = xaml[badgeStart..xaml.IndexOf("<TextBlock x:Name=\"ModeStatusText\"", badgeStart, StringComparison.Ordinal)];
        Assert.Contains("DockPanel.Dock=\"Left\"", badgeBlock);
        Assert.Contains("HorizontalAlignment=\"Left\"", badgeBlock);
        Assert.Contains("MaxWidth=\"360\"", badgeBlock);
        Assert.Contains("CornerRadius=\"8\"", badgeBlock);
        Assert.DoesNotContain("CornerRadius=\"999\"", badgeBlock);
    }

    [Fact]
    public void Map_mode_switch_keeps_current_view_state()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        var setModeStart = code.IndexOf("private bool SetMapMode", StringComparison.Ordinal);
        var importStart = code.IndexOf("private void ImportMapButton_Click", StringComparison.Ordinal);

        Assert.True(setModeStart >= 0);
        Assert.True(importStart > setModeStart);

        var setModeBlock = code[setModeStart..importStart];
        Assert.Contains("RenderCurrentMap(resetView: false);", setModeBlock);
        Assert.Contains("UpdateMapModeVisual();", setModeBlock);
        Assert.DoesNotContain("hasUserViewState = false;", setModeBlock);
        Assert.DoesNotContain("RequestFitMapToView();", setModeBlock);
    }

    [Fact]
    public void Edge_segment_menu_actions_save_layout_after_mutation()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        var addStart = code.IndexOf("private void AddEdgeDetour_Click", StringComparison.Ordinal);
        var clearStart = code.IndexOf("private void ClearEdgePoints_Click", StringComparison.Ordinal);
        var deleteStart = code.IndexOf("private void DeleteEdge_Click", StringComparison.Ordinal);

        Assert.True(addStart >= 0);
        Assert.True(clearStart > addStart);
        Assert.True(deleteStart > clearStart);

        var addBlock = code[addStart..clearStart];
        var clearBlock = code[clearStart..deleteStart];
        Assert.Contains("saveLayout(currentMap)", addBlock);
        Assert.Contains("saveLayout(currentMap)", clearBlock);
    }

    [Theory]
    [InlineData(5, 5, 0, 0, 10, 0, 25)]
    [InlineData(-5, 0, 0, 0, 10, 0, 25)]
    [InlineData(20, 0, 0, 0, 10, 0, 100)]
    [InlineData(3, 4, 0, 0, 0, 0, 25)]
    public void Point_to_segment_distance_handles_projection_and_degenerate_segments(
        double pointX,
        double pointY,
        double startX,
        double startY,
        double endX,
        double endY,
        double expected)
    {
        var distance = FactoryMapWindow.CalculatePointToSegmentDistanceSquared(
            new Point(pointX, pointY),
            new Point(startX, startY),
            new Point(endX, endY));

        Assert.Equal(expected, distance, precision: 6);
    }

    [Fact]
    public void Edge_point_insert_index_returns_zero_without_manual_points()
    {
        var edge = CreateEdgeWithoutManualPoints();

        var index = FactoryMapWindow.GetInsertPointIndex(edge, new Point(180, 120));

        Assert.Equal(0, index);
    }

    [Fact]
    public void Edge_point_insert_index_uses_nearest_orthogonal_manual_segment()
    {
        var edge = new FactoryMapDeviceEdgeViewData
        {
            From = new FactoryMapDeviceViewNode { X = 100, Y = 100 },
            To = new FactoryMapDeviceViewNode { X = 360, Y = 100 },
            Points =
            [
                new FactoryMapPoint { X = 300, Y = 129 },
                new FactoryMapPoint { X = 300, Y = 180 },
                new FactoryMapPoint { X = 360, Y = 180 }
            ]
        };

        var beforeManualPoint = FactoryMapWindow.GetInsertPointIndex(edge, new Point(275, 130));
        var afterManualPoint = FactoryMapWindow.GetInsertPointIndex(edge, new Point(330, 182));

        Assert.Equal(0, beforeManualPoint);
        Assert.Equal(2, afterManualPoint);
    }

    [Fact]
    public void Edge_point_snap_uses_map_grid_size()
    {
        var point = FactoryMapWindow.SnapEdgePointToGrid(new Point(124, 126));

        Assert.Equal(120, point.X);
        Assert.Equal(130, point.Y);
    }

    [Fact]
    public void Edge_point_index_validation_rejects_out_of_range_and_invalid_points()
    {
        var edge = new FactoryMapDeviceEdgeViewData
        {
            Points =
            [
                new FactoryMapPoint { X = 100, Y = 120 },
                new FactoryMapPoint { X = double.NaN, Y = 130 },
                new FactoryMapPoint { X = 140, Y = double.PositiveInfinity }
            ]
        };

        Assert.False(FactoryMapWindow.IsValidEdgePointIndex(edge, -1));
        Assert.True(FactoryMapWindow.IsValidEdgePointIndex(edge, 0));
        Assert.False(FactoryMapWindow.IsValidEdgePointIndex(edge, 1));
        Assert.False(FactoryMapWindow.IsValidEdgePointIndex(edge, 2));
        Assert.False(FactoryMapWindow.IsValidEdgePointIndex(edge, 3));
    }

    [Fact]
    public void Edge_point_handle_interactions_are_wired_for_selection_drag_and_delete()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        Assert.Contains("private FactoryMapDeviceEdgeViewData? selectedEdge;", code);
        Assert.Contains("private FactoryMapDeviceEdgeViewData? activeEdgePointEdge;", code);
        Assert.Contains("private int activeEdgePointIndex = -1;", code);
        Assert.Contains("private bool isDraggingEdgePoint;", code);
        Assert.Contains("private sealed record EdgePointHandlePayload", code);

        Assert.Contains("hitPolyline.PreviewMouseLeftButtonDown += Edge_PreviewMouseLeftButtonDown;", code);
        Assert.DoesNotContain("DrawSelectedEdgePointHandles();", code);
        Assert.Contains("handle.MouseLeftButtonDown += EdgePointHandle_MouseLeftButtonDown;", code);
        Assert.Contains("handle.MouseMove += EdgePointHandle_MouseMove;", code);
        Assert.Contains("handle.MouseLeftButtonUp += EdgePointHandle_MouseLeftButtonUp;", code);
        Assert.Contains("handle.PreviewMouseRightButtonDown += EdgePointHandle_PreviewMouseRightButtonDown;", code);
    }

    [Fact]
    public void Edge_point_handle_mutations_snap_save_and_clear_state()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        var endDragStart = code.IndexOf("private void EndEdgePointDrag", StringComparison.Ordinal);
        var deletePointStart = code.IndexOf("private void DeleteEdgePoint_Click", StringComparison.Ordinal);
        var deleteEdgeStart = code.IndexOf("private void DeleteEdge_Click", StringComparison.Ordinal);
        var editModeStart = code.IndexOf("private void EditModeButton_Click", StringComparison.Ordinal);
        var importStart = code.IndexOf("private void ImportMapButton_Click", StringComparison.Ordinal);

        Assert.True(endDragStart >= 0);
        Assert.True(deletePointStart > endDragStart);
        Assert.True(deleteEdgeStart > deletePointStart);
        Assert.True(editModeStart >= 0);
        Assert.True(importStart > editModeStart);

        var endDragBlock = code[endDragStart..deletePointStart];
        var deletePointBlock = code[deletePointStart..deleteEdgeStart];
        var deleteEdgeBlock = code[deleteEdgeStart..editModeStart];
        var editModeBlock = code[editModeStart..importStart];

        Assert.Contains("saveLayout(currentMap)", endDragBlock);
        Assert.Contains("FactoryMapOrthogonalPathService.InsertDetourOnSegment", code);
        Assert.Contains("FactoryMapOrthogonalPathService.MovePoint", code);
        Assert.Contains("FactoryMapOrthogonalPathService.Normalize", code);
        Assert.Contains("edge.Points.RemoveAt(pointIndex)", deletePointBlock);
        Assert.Contains("saveLayout(currentMap)", deletePointBlock);
        Assert.Contains("if (ReferenceEquals(selectedEdge, edge))", deleteEdgeBlock);
        Assert.Contains("ClearEdgeSelection();", editModeBlock);
        Assert.Contains("ClearEdgePointDragState();", editModeBlock);
    }

    [Fact]
    public void Factory_map_window_can_initialize_with_application_resources()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            Application? ownedApplication = null;
            try
            {
                ownedApplication = EnsureApplicationResources();
                var window = new FactoryMapWindow(
                    _ => { },
                    _ => Array.Empty<ContextMenuCapabilityDefinition>(),
                    (_, _) => Task.CompletedTask,
                    _ => { },
                    _ => { },
                    _ => true,
                    () => Array.Empty<ShortcutItem>(),
                    () => Path.Combine(Path.GetTempPath(), "factory-map-runtime-test.json"));
                window.RenderMap(new FactoryMapDeviceViewData
                {
                    TopologyAuthoritative = true,
                    Devices =
                    [
                        new FactoryMapDeviceViewNode
                        {
                            Id = "node-a",
                            Key = "A",
                            Name = "设备A",
                            X = 100,
                            Y = 100
                        }
                    ],
                    ConnectionPoints =
                    [
                        new FactoryMapConnectionPoint
                        {
                            Id = "free-1",
                            Kind = FactoryMapConnectionPointKinds.Free,
                            X = 400,
                            Y = 129
                        }
                    ],
                    Segments =
                    [
                        new FactoryMapSegment
                        {
                            Id = "segment-1",
                            FromPointId = "node-a:right",
                            ToPointId = "free-1",
                            ZIndex = 1
                        }
                    ]
                });
                window.Close();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                ownedApplication?.Shutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    private static Application? EnsureApplicationResources()
    {
        var ownedApplication = Application.Current is null ? new Application() : null;
        var application = Application.Current ?? ownedApplication!;
        application.Resources.MergedDictionaries.Clear();
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(TestProjectPaths.GetProjectFilePath(
                "VSLoader",
                "Styles",
                "ModernTheme.xaml"), UriKind.Absolute)
        });
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(TestProjectPaths.GetProjectFilePath(
                "VSLoader",
                "Styles",
                "ModernWindowChrome.xaml"), UriKind.Absolute)
        });
        application.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
        return ownedApplication;
    }

    private static FactoryMapDeviceEdgeViewData CreateEdgeWithoutManualPoints()
    {
        return new FactoryMapDeviceEdgeViewData
        {
            From = new FactoryMapDeviceViewNode { X = 100, Y = 100 },
            To = new FactoryMapDeviceViewNode { X = 360, Y = 120 }
        };
    }

    private static void AssertOrthogonal(PointCollection points)
    {
        for (var i = 0; i < points.Count - 1; i++)
        {
            Assert.True(
                Math.Abs(points[i].X - points[i + 1].X) < 0.001
                || Math.Abs(points[i].Y - points[i + 1].Y) < 0.001,
                $"Segment {i} is diagonal: ({points[i].X},{points[i].Y}) -> ({points[i + 1].X},{points[i + 1].Y})");
        }
    }
}
