using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using VSLoader.Behaviors;
using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfCursor = System.Windows.Input.Cursor;
using WpfCursors = System.Windows.Input.Cursors;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace VSLoader.Views;

public partial class FactoryMapWindow : Window
{
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MOUSEHWHEEL = 0x020E;
    private const int WH_MOUSE_LL = 14;
    private const double DeviceWidth = FactoryMapNodeGeometryService.MinimumWidth;
    private const double DeviceHeight = FactoryMapNodeGeometryService.MinimumHeight;
    internal const double EdgeEndpointInset = 3;
    private const double ConnectorSize = 10;
    private const double SelectedConnectorSize = 12;
    private const double EdgePointHandleSize = 12;
    private const double DragThreshold = 4;
    private const double SnapGridSize = 10;
    private const double ViewPadding = 28;
    private const double EditCanvasBuffer = 500;
    private const double MajorGridMultiplier = 5;
    private const double ZoomFactor = 1.1;
    private const double MinUserScale = 0.5;
    private const double MaxUserScale = 4.0;
    private const double EdgeMergePrecision = 1000;
    private const double EdgeMergeTolerance = 0.001;
    private const int MaxGridLineCount = 2000;
    private const int MaxFitRetryCount = 12;
    private readonly Action<ShortcutItem> selectShortcut;
    private readonly Func<string, IReadOnlyList<ContextMenuCapabilityDefinition>> getContextMenuCapabilities;
    private readonly Func<ShortcutItem, ContextMenuCapabilityDefinition, Task> executeContextMenuCapability;
    private readonly Action<ShortcutItem> editShortcut;
    private readonly Action<ShortcutItem> deleteShortcut;
    private readonly Func<FactoryMapDeviceViewData, bool> saveLayout;
    private readonly Func<IReadOnlyList<ShortcutItem>> getCurrentShortcuts;
    private readonly Func<string> getLayoutPath;
    private readonly Action<string>? mapImported;
    private readonly Action downloadAdminUiLinks;
    private readonly DialogService dialogService = new();
    private readonly FactoryMapLayoutService layoutService = new();
    private readonly FactoryMapTopologyService topologyService = new();
    private readonly FactoryMapConnectionDraftService connectionDraftService = new();
    private readonly FactoryMapMovementService movementService = new();
    private readonly FactoryMapLineArrangementService lineArrangementService = new();
    private readonly FactoryMapRenderIndexService renderIndexService = new();
    private readonly FactoryMapMarqueeSelectionService marqueeSelectionService = new();
    private readonly FactoryMapSelectionState topologySelection = new();
    private readonly FactoryMapInteractionState interactionState = new();
    private readonly DispatcherTimer topologySaveTimer;
    private readonly Dictionary<Border, FactoryMapDeviceViewNode> deviceByElement = [];
    private readonly Dictionary<FrameworkElement, FactoryMapConnectorViewNode> connectorByElement = [];
    private readonly Dictionary<FactoryMapDeviceViewNode, Border> elementByDevice = [];
    private readonly Dictionary<FactoryMapConnectorViewNode, FrameworkElement> elementByConnector = [];
    private readonly Dictionary<FactoryMapObjectRef, WpfPoint> multiDragStartPositions = [];
    private readonly Dictionary<string, FrameworkElement> topologyPointElementById = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FactoryMapObjectRef> marqueeBaseSelection = [];
    private bool isDraggingMap;
    private bool isMiddleButtonPanning;
    private bool isDraggingDevice;
    private bool isDraggingConnector;
    private bool isDraggingEdgePoint;
    private bool isDraggingSelection;
    private bool isEditMode => interactionState.Mode == FactoryMapMode.Edit;
    private bool isMarqueeSelecting;
    private bool isSelectionPointerDown;
    private bool selectionAddsToExisting;
    private bool hasSelectionDragStarted;
    private bool hasDeviceDragStarted;
    private bool hasUserViewState;
    private bool pendingFitToView;
    private bool isHandlingDeactivation;
    private int fitRetryCount;
    private Border? activeDeviceElement;
    private FrameworkElement? activeConnectorElement;
    private WpfRectangle? selectionRectangle;
    private FactoryMapDeviceViewData? currentMap;
    private FactoryMapDeviceEdgeViewData? selectedEdge;
    private FactoryMapDeviceEdgeViewData? selectedSegmentEdge;
    private FactoryMapDeviceEdgeViewData? activeSegmentEdge;
    private FactoryMapDeviceEdgeViewData? activeEdgePointEdge;
    private FactoryMapDeviceViewNode? activeDevice;
    private FactoryMapConnectorViewNode? activeConnector;
    private FactoryMapConnectorViewNode? selectedConnector;
    private PendingConnectionEndpoint? pendingConnectionStart;
    private int selectedSegmentIndex = -1;
    private int activeSegmentIndex = -1;
    private int activeEdgePointIndex = -1;
    private bool isDraggingEdgeSegment;
    private string baseStatusText = "地图未加载";
    private string? highlightedShortcutKey;
    private IntPtr lowLevelMouseHookHandle;
    private LowLevelMouseProc? lowLevelMouseProc;
    private double fitScale = 1.0;
    private WpfPoint dragStartPoint;
    private WpfPoint lastDragPoint;
    private WpfPoint selectionStartPoint;
    private WpfPoint selectionStartViewportPoint;
    private Vector multiDragMapDelta;
    private double mapOffsetX;
    private double mapOffsetY;
    private double userScale = 1.0;
    private string? pendingTopologyPointId
    {
        get => interactionState.PendingConnectionPointId;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                interactionState.CancelConnectionDraft();
                return;
            }

            interactionState.BeginConnectionDraft(value);
        }
    }
    private bool HasTopologyConnectionDraft => interactionState.ConnectionDraft is not null;
    private string? activeTopologyPointId;
    private string? activeTopologySegmentId;
    private FrameworkElement? activeTopologyPointElement;
    private bool isDraggingTopologyPoint;
    private bool isDraggingTopologySegment;
    private WpfPoint topologyDragStartMapPoint;
    private double topologyPointStartX;
    private double topologyPointStartY;
    private double activeDeviceStartX;
    private double activeDeviceStartY;
    private TopologySnapshot? pendingTopologySaveSnapshot;
    private string pendingTopologySaveErrorMessage = "地图对象移动后保存失败。";
    private CancellationTokenSource? arrangeLinesCancellationTokenSource;
    private long arrangeLinesOperationVersion;
    private long mapFocusRestoreGeneration;
    private bool isArrangingLines;
    private bool isWindowClosed;

    private sealed record EdgeContextMenuPayload(
        FactoryMapDeviceEdgeViewData Edge,
        WpfPoint ClickPoint,
        int SegmentIndex);

    internal sealed record FactoryMapVisibleEdgeSegment(WpfPoint Start, WpfPoint End);

    private sealed record NormalizedVisibleEdgeSegment(
        bool IsHorizontal,
        long AxisKey,
        double From,
        double To);

    private sealed record EdgePointHandlePayload(
        FactoryMapDeviceEdgeViewData Edge,
        int PointIndex);

    private sealed record EndpointPortPayload(
        FactoryMapEndpointViewData Endpoint,
        string Port,
        string DisplayName);

    private sealed record PendingConnectionEndpoint(
        FactoryMapEndpointViewData Endpoint,
        string Port,
        string DisplayName);

    private sealed record TopologySnapshot(
        List<FactoryMapConnectionPoint> Points,
        List<FactoryMapSegment> Segments,
        Dictionary<string, WpfPoint> DevicePositions);

    public FactoryMapWindow(
        Action<ShortcutItem> selectShortcut,
        Func<string, IReadOnlyList<ContextMenuCapabilityDefinition>> getContextMenuCapabilities,
        Func<ShortcutItem, ContextMenuCapabilityDefinition, Task> executeContextMenuCapability,
        Action<ShortcutItem> editShortcut,
        Action<ShortcutItem> deleteShortcut,
        Func<FactoryMapDeviceViewData, bool> saveLayout,
        Func<IReadOnlyList<ShortcutItem>> getCurrentShortcuts,
        Func<string> getLayoutPath,
        Action? downloadAdminUiLinks = null,
        Action<string>? mapImported = null)
    {
        this.selectShortcut = selectShortcut;
        this.getContextMenuCapabilities = getContextMenuCapabilities;
        this.executeContextMenuCapability = executeContextMenuCapability;
        this.editShortcut = editShortcut;
        this.deleteShortcut = deleteShortcut;
        this.saveLayout = saveLayout;
        this.getCurrentShortcuts = getCurrentShortcuts;
        this.getLayoutPath = getLayoutPath;
        this.downloadAdminUiLinks = downloadAdminUiLinks ?? (() => { });
        this.mapImported = mapImported;
        InitializeComponent();
        UpdateMapModeVisual();
        topologySaveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        topologySaveTimer.Tick += (_, _) =>
        {
            FlushPendingTopologySave();
        };
        MapTitleBar.CloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        AddHandler(Mouse.PreviewMouseWheelEvent, new MouseWheelEventHandler(FactoryMapWindow_PreviewMouseWheel), handledEventsToo: true);
        SourceInitialized += FactoryMapWindow_SourceInitialized;
        Deactivated += FactoryMapWindow_Deactivated;
        Closed += (_, _) =>
        {
            isWindowClosed = true;
            CancelArrangeLinesOperation();
            UninstallLowLevelMouseHook();
        };
        Closing += (_, e) =>
        {
            CancelArrangeLinesOperation();
            if (!FlushPendingTopologySave())
            {
                e.Cancel = true;
            }
        };
        Loaded += (_, _) => RequestFitMapToView();
        ContentRendered += (_, _) => RequestFitMapToView();
        Focusable = true;
        PreviewKeyDown += FactoryMapWindow_PreviewKeyDown;
    }

    public event EventHandler? ViewStateChanged;

    public event EventHandler? CloseRequested;

    public bool HasUserViewState => hasUserViewState;

    internal static double DeviceNodeWidth => DeviceWidth;

    internal static double DeviceNodeHeight => DeviceHeight;

    internal static double MapGridSize => SnapGridSize;

    internal static double MapMajorGridSize => SnapGridSize * MajorGridMultiplier;

    internal static bool ShouldInvokeDownloadAdminUiLinks(bool canExecute)
    {
        return canExecute;
    }

    internal static string FormatDebugStatusText(
        string baseText,
        double viewportWidth,
        double viewportHeight,
        double scale,
        string bounds)
    {
        return $"{baseText} | 视口:{viewportWidth:0}x{viewportHeight:0} | 缩放:{scale:0.###} | 边界:{bounds}";
    }

    public void RenderMap(FactoryMapDeviceViewData map)
    {
        RenderMap(map, resetView: true);
    }

    public void RenderMap(FactoryMapDeviceViewData map, bool resetView)
    {
        if (isArrangingLines)
        {
            CancelArrangeLinesOperation();
        }

        CompleteActivePointerInteraction();
        if (!FlushPendingTopologySave())
        {
            return;
        }
        EnsureTopologyRuntime(map);
        topologySelection.Clear();
        ClearEdgeSelection();
        pendingTopologyPointId = null;
        pendingConnectionStart = null;
        currentMap = map;
        if (resetView)
        {
            hasUserViewState = false;
        }

        RenderCurrentMap(resetView);
        RefreshArrangeLinesButtonState();
    }

    private static void EnsureTopologyRuntime(FactoryMapDeviceViewData map)
    {
        if (map.TopologyAuthoritative)
        {
            return;
        }

        var usedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in map.Devices)
        {
            var baseId = string.IsNullOrWhiteSpace(device.Id)
                ? FactoryMapLayoutTopologyConverter.CreateStableNodeId(device.Key)
                : device.Id.Trim();
            var id = baseId;
            var suffix = 2;
            while (!usedNodeIds.Add(id))
            {
                id = $"{baseId}-{suffix++}";
            }

            device.Id = id;
        }

        if (map.ConnectionPoints.Count == 0 && map.Segments.Count == 0)
        {
            var topology = FactoryMapLayoutTopologyConverter.BuildFromLegacy(
                map.Devices,
                map.Connectors,
                map.Edges);
            map.ConnectionPoints = topology.Points;
            map.Segments = topology.Segments;
            map.InvalidSegmentCount = topology.InvalidSegmentCount;
        }

        map.TopologyAuthoritative = true;
    }

    public FactoryMapViewState CaptureViewState()
    {
        return new FactoryMapViewState
        {
            FitScale = fitScale,
            UserScale = userScale,
            OffsetX = mapOffsetX,
            OffsetY = mapOffsetY
        };
    }

    public void RestoreViewState(FactoryMapViewState? state)
    {
        if (state is null)
        {
            return;
        }

        fitScale = state.FitScale > 0 ? state.FitScale : 1.0;
        userScale = state.UserScale > 0 ? state.UserScale : 1.0;
        mapOffsetX = state.OffsetX;
        mapOffsetY = state.OffsetY;
        hasUserViewState = true;
        pendingFitToView = false;
        ApplyMapTransform();
        RefreshStatusText();
    }

    public void RestoreMapInputFocus()
    {
        RestoreMapFocusAfterToolbarClick();
    }

    public void CancelPendingInputFocusRestore()
    {
        mapFocusRestoreGeneration++;
    }

    public void ShowError(string message)
    {
        currentMap = new FactoryMapDeviceViewData
        {
            Canvas = new FactoryMapCanvas { Width = 580, Height = 360 }
        };
        MapCanvas.Children.Clear();
        deviceByElement.Clear();
        topologyPointElementById.Clear();
        MapCanvas.Width = 580;
        MapCanvas.Height = 360;
        RequestFitMapToView();
        SetStatusText(message);
    }

    public void HighlightShortcut(ShortcutItem? shortcut)
    {
        highlightedShortcutKey = string.IsNullOrWhiteSpace(shortcut?.TargetPath)
            ? null
            : shortcut.TargetPath.Trim();
        RefreshDeviceSelectionVisuals();
    }

    private void RenderCurrentMap(bool resetView)
    {
        if (currentMap is null)
        {
            return;
        }

        if (currentMap.TopologyAuthoritative)
        {
            RenderTopologyMap(resetView);
            return;
        }

        MapCanvas.Children.Clear();
        selectionRectangle = null;
        deviceByElement.Clear();
        elementByDevice.Clear();
        connectorByElement.Clear();
        elementByConnector.Clear();
        topologyPointElementById.Clear();
        var canvasSize = GetEffectiveCanvasSize();
        MapCanvas.Width = canvasSize.Width;
        MapCanvas.Height = canvasSize.Height;

        DrawGridIfNeeded();

        DrawVisibleEdges(currentMap.Edges);

        foreach (var edge in currentMap.Edges)
        {
            DrawEdgeHitTarget(edge);
        }

        foreach (var connector in currentMap.Connectors)
        {
            DrawConnector(connector);
        }

        foreach (var device in currentMap.Devices)
        {
            DrawDevice(device);
        }

        foreach (var connector in currentMap.Connectors)
        {
            DrawEndpointPorts(FactoryMapEndpointViewData.FromConnector(connector), "连接点");
        }

        foreach (var device in currentMap.Devices)
        {
        DrawEndpointPorts(FactoryMapEndpointViewData.FromDevice(device), device.Name);
        }

        DrawSelectedEdgeSegmentHighlight();

        var statusParts = new List<string>
        {
            $"设备：{currentMap.Devices.Count}",
            $"连线：{currentMap.Edges.Count}"
        };

        if (currentMap.InvalidEdgeCount > 0)
        {
            statusParts.Add($"无效连线：{currentMap.InvalidEdgeCount}");
        }

        SetStatusText(string.Join("  |  ", statusParts));
        if (resetView)
        {
            RequestFitMapToView();
        }
        else
        {
            ApplyMapTransform();
            RefreshStatusText();
        }
    }

    private void RenderTopologyMap(bool resetView)
    {
        if (currentMap is null)
        {
            return;
        }

        SynchronizeAttachedPoints(currentMap);
        MapCanvas.Children.Clear();
        selectionRectangle = null;
        deviceByElement.Clear();
        elementByDevice.Clear();
        connectorByElement.Clear();
        elementByConnector.Clear();
        topologyPointElementById.Clear();
        var canvasSize = GetEffectiveCanvasSize();
        MapCanvas.Width = canvasSize.Width;
        MapCanvas.Height = canvasSize.Height;
        DrawGridIfNeeded();

        var selectedSegmentId = topologySelection.PrimaryObject is { Kind: FactoryMapObjectKind.Segment } selectedSegment
            ? selectedSegment.Id
            : null;
        foreach (var visibleSegment in renderIndexService.Build(
                     currentMap.ConnectionPoints,
                     currentMap.Segments,
                     selectedSegmentId))
        {
            DrawTopologyVisibleSegment(visibleSegment);
        }

        foreach (var device in currentMap.Devices)
        {
            DrawDevice(device);
        }

        foreach (var point in currentMap.ConnectionPoints)
        {
            if (ShouldDrawTopologyPoint(point))
            {
                DrawTopologyPoint(point);
            }
        }
        DrawTopologyConnectionDraftPreview();

        var statusParts = new List<string>
        {
            $"设备：{currentMap.Devices.Count}",
            $"连接点：{currentMap.ConnectionPoints.Count(point => point.Kind != FactoryMapConnectionPointKinds.Attached)}",
            $"线段：{currentMap.Segments.Count}"
        };
        if (currentMap.InvalidSegmentCount > 0)
        {
            statusParts.Add($"无效线段：{currentMap.InvalidSegmentCount}");
        }

        SetStatusText(string.Join("  |  ", statusParts));
        if (resetView)
        {
            RequestFitMapToView();
        }
        else
        {
            ApplyMapTransform();
            RefreshStatusText();
        }
    }

    private void DrawTopologyVisibleSegment(FactoryMapVisibleSegment visibleSegment)
    {
        var isSelected = topologySelection.PrimaryObject is { Kind: FactoryMapObjectKind.Segment } selected
            && visibleSegment.SourceSegmentIds.Contains(selected.Id, StringComparer.OrdinalIgnoreCase);
        var line = new Line
        {
            X1 = visibleSegment.Start.X,
            Y1 = visibleSegment.Start.Y,
            X2 = visibleSegment.End.X,
            Y2 = visibleSegment.End.Y,
            Stroke = new SolidColorBrush(isSelected
                ? WpfColor.FromRgb(37, 99, 235)
                : WpfColor.FromRgb(148, 163, 184)),
            StrokeThickness = isSelected ? 4 : 2,
            StrokeStartLineCap = PenLineCap.Flat,
            StrokeEndLineCap = PenLineCap.Flat,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
        MapCanvas.Children.Add(line);

        var hitLine = new Line
        {
            X1 = visibleSegment.Start.X,
            Y1 = visibleSegment.Start.Y,
            X2 = visibleSegment.End.X,
            Y2 = visibleSegment.End.Y,
            Stroke = WpfBrushes.Transparent,
            StrokeThickness = 12,
            Tag = visibleSegment,
            Cursor = isEditMode ? WpfCursors.Hand : WpfCursors.Arrow
        };
        hitLine.PreviewMouseLeftButtonDown += TopologySegment_PreviewMouseLeftButtonDown;
        hitLine.PreviewMouseRightButtonDown += TopologySegment_PreviewMouseRightButtonDown;
        MapCanvas.Children.Add(hitLine);
    }

    private bool ShouldDrawTopologyPoint(FactoryMapConnectionPoint point)
    {
        if (point.Kind is FactoryMapConnectionPointKinds.Free or FactoryMapConnectionPointKinds.Junction)
        {
            return true;
        }

        if (!isEditMode)
        {
            return false;
        }

        if (point.Kind == FactoryMapConnectionPointKinds.Attached)
        {
            return true;
        }

        return topologySelection.Contains(new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, point.Id))
            || (topologySelection.PrimaryObject is { Kind: FactoryMapObjectKind.Segment } selectedSegment
                && currentMap?.Segments.Any(segment =>
                    string.Equals(segment.Id, selectedSegment.Id, StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(segment.FromPointId, point.Id, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(segment.ToPointId, point.Id, StringComparison.OrdinalIgnoreCase))) == true);
    }

    private void DrawTopologyPoint(FactoryMapConnectionPoint point)
    {
        var objectRef = new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, point.Id);
        var isSelected = topologySelection.Contains(objectRef);
        var isPending = string.Equals(pendingTopologyPointId, point.Id, StringComparison.OrdinalIgnoreCase);
        var size = point.Kind switch
        {
            FactoryMapConnectionPointKinds.Attached => 8d,
            FactoryMapConnectionPointKinds.Bend => 7d,
            FactoryMapConnectionPointKinds.Junction => 9d,
            _ => 10d
        };
        if (isSelected || isPending)
        {
            size += 2;
        }

        var fill = point.Kind switch
        {
            FactoryMapConnectionPointKinds.Bend => new SolidColorBrush(WpfColor.FromRgb(226, 232, 240)),
            FactoryMapConnectionPointKinds.Attached => WpfBrushes.White,
            FactoryMapConnectionPointKinds.Junction => new SolidColorBrush(WpfColor.FromRgb(219, 234, 254)),
            _ => new SolidColorBrush(WpfColor.FromRgb(239, 246, 255))
        };
        Shape element = point.Kind == FactoryMapConnectionPointKinds.Junction
            ? new WpfRectangle
            {
                RenderTransform = new RotateTransform(45),
                RenderTransformOrigin = new WpfPoint(0.5, 0.5)
            }
            : new Ellipse();
        element.Width = size;
        element.Height = size;
        element.Fill = fill;
        element.Stroke = new SolidColorBrush(isPending
            ? WpfColor.FromRgb(22, 163, 74)
            : isSelected
                ? WpfColor.FromRgb(37, 99, 235)
                : WpfColor.FromRgb(59, 130, 246));
        element.StrokeThickness = isSelected || isPending ? 2.5 : 1.5;
        element.Tag = point.Id;
        element.Cursor = isEditMode ? GetTopologyPointCursor(point) : WpfCursors.Arrow;
        element.ToolTip = GetTopologyPointDisplayName(point);
        element.SnapsToDevicePixels = true;
        element.PreviewMouseLeftButtonDown += TopologyPoint_PreviewMouseLeftButtonDown;
        element.PreviewMouseRightButtonDown += TopologyPoint_PreviewMouseRightButtonDown;
        topologyPointElementById[point.Id] = element;
        Canvas.SetLeft(element, point.X - size / 2);
        Canvas.SetTop(element, point.Y - size / 2);
        MapCanvas.Children.Add(element);
    }

    private static string GetTopologyPointDisplayName(FactoryMapConnectionPoint point)
    {
        return point.Kind switch
        {
            FactoryMapConnectionPointKinds.Attached => $"节点连接点：{GetPortDisplayName(point.Side)}",
            FactoryMapConnectionPointKinds.Bend => "折弯点",
            FactoryMapConnectionPointKinds.Junction => "分支连接点",
            _ => "普通连接点"
        };
    }

    private static WpfCursor GetTopologyPointCursor(FactoryMapConnectionPoint point)
    {
        if (point.Kind != FactoryMapConnectionPointKinds.Junction)
        {
            return point.Kind == FactoryMapConnectionPointKinds.Attached
                ? WpfCursors.Hand
                : WpfCursors.SizeAll;
        }

        return FactoryMapJunctionAxes.Normalize(point.JunctionAxis) switch
        {
            FactoryMapJunctionAxes.Horizontal => WpfCursors.SizeWE,
            FactoryMapJunctionAxes.Vertical => WpfCursors.SizeNS,
            FactoryMapJunctionAxes.Locked => WpfCursors.No,
            _ => WpfCursors.No
        };
    }

    private void DrawTopologyConnectionDraftPreview()
    {
        var draft = interactionState.ConnectionDraft;
        if (!isEditMode || draft?.OriginKind != FactoryMapConnectionOriginKinds.Segment)
        {
            return;
        }

        const double size = 9;
        var preview = new WpfRectangle
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(WpfColor.FromRgb(220, 252, 231)),
            Stroke = new SolidColorBrush(WpfColor.FromRgb(22, 163, 74)),
            StrokeThickness = 2,
            RenderTransform = new RotateTransform(45),
            RenderTransformOrigin = new WpfPoint(0.5, 0.5),
            IsHitTestVisible = false,
            ToolTip = "分支连接点预览",
            SnapsToDevicePixels = true
        };
        Canvas.SetLeft(preview, draft.SegmentX - size / 2);
        Canvas.SetTop(preview, draft.SegmentY - size / 2);
        System.Windows.Controls.Panel.SetZIndex(preview, 40);
        MapCanvas.Children.Add(preview);
    }

    private static void SynchronizeAttachedPoints(FactoryMapDeviceViewData map)
    {
        FactoryMapNodeGeometryService.SynchronizeAttachedPoints(map);
    }

    private void DrawVisibleEdges(IReadOnlyList<FactoryMapDeviceEdgeViewData> edges)
    {
        foreach (var segment in CreateMergedVisibleEdgeSegments(edges))
        {
            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(WpfColor.FromRgb(148, 163, 184)),
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Flat,
                Points = new PointCollection { segment.Start, segment.End }
            };

            MapCanvas.Children.Add(polyline);
        }
    }

    private void DrawEdgeHitTarget(FactoryMapDeviceEdgeViewData edge)
    {
        var hitPoints = CreateEdgePoints(edge);

        var hitPolyline = new Polyline
        {
            Stroke = WpfBrushes.Transparent,
            StrokeThickness = 12,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Points = hitPoints,
            Tag = edge
        };
        hitPolyline.PreviewMouseRightButtonDown += Edge_PreviewMouseRightButtonDown;
        hitPolyline.PreviewMouseLeftButtonDown += Edge_PreviewMouseLeftButtonDown;
        MapCanvas.Children.Add(hitPolyline);
    }

    private void DrawSelectedEdgeSegmentHighlight()
    {
        if (!isEditMode
            || selectedSegmentEdge is null
            || selectedSegmentIndex < 0
            || currentMap is null
            || !currentMap.Edges.Contains(selectedSegmentEdge))
        {
            return;
        }

        var path = GetEditableEdgePath(selectedSegmentEdge);
        if (selectedSegmentIndex >= path.Count - 1)
        {
            return;
        }

        var highlight = new Polyline
        {
            Stroke = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235)),
            StrokeThickness = 5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = 0.85,
            IsHitTestVisible = false,
            Points = new PointCollection
            {
                path[selectedSegmentIndex],
                path[selectedSegmentIndex + 1]
            }
        };

        MapCanvas.Children.Add(highlight);
    }

    private void DrawSelectedEdgePointHandles()
    {
        if (!isEditMode || selectedEdge is null || currentMap is null)
        {
            return;
        }

        if (!currentMap.Edges.Contains(selectedEdge))
        {
            ClearEdgeSelection();
            return;
        }

        for (var i = 0; i < selectedEdge.Points.Count; i++)
        {
            var point = selectedEdge.Points[i];
            if (!IsValidEdgePointIndex(selectedEdge, i))
            {
                continue;
            }

            MapCanvas.Children.Add(CreateEdgePointHandle(selectedEdge, point, i));
        }
    }

    private FrameworkElement CreateEdgePointHandle(
        FactoryMapDeviceEdgeViewData edge,
        FactoryMapPoint point,
        int pointIndex)
    {
        var handle = new Ellipse
        {
            Width = EdgePointHandleSize,
            Height = EdgePointHandleSize,
            Fill = WpfBrushes.White,
            Stroke = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235)),
            StrokeThickness = 2,
            Cursor = WpfCursors.SizeAll,
            Tag = new EdgePointHandlePayload(edge, pointIndex),
            SnapsToDevicePixels = true
        };

        handle.MouseEnter += (_, _) =>
        {
            handle.Fill = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235));
            handle.Stroke = WpfBrushes.White;
        };
        handle.MouseLeave += (_, _) =>
        {
            if (!isDraggingEdgePoint)
            {
                handle.Fill = WpfBrushes.White;
                handle.Stroke = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235));
            }
        };
        handle.MouseLeftButtonDown += EdgePointHandle_MouseLeftButtonDown;
        handle.MouseMove += EdgePointHandle_MouseMove;
        handle.MouseLeftButtonUp += EdgePointHandle_MouseLeftButtonUp;
        handle.PreviewMouseRightButtonDown += EdgePointHandle_PreviewMouseRightButtonDown;

        Canvas.SetLeft(handle, point.X - EdgePointHandleSize / 2);
        Canvas.SetTop(handle, point.Y - EdgePointHandleSize / 2);
        return handle;
    }

    private void DrawGridIfNeeded()
    {
        if (!isEditMode || MapCanvas.Width <= 0 || MapCanvas.Height <= 0)
        {
            return;
        }

        var gridSize = MapGridSize;
        if (GetGridLineCount(MapCanvas.Width, MapCanvas.Height, gridSize) > MaxGridLineCount)
        {
            gridSize = MapMajorGridSize;
        }

        if (GetGridLineCount(MapCanvas.Width, MapCanvas.Height, gridSize) > MaxGridLineCount)
        {
            return;
        }

        var minorBrush = new SolidColorBrush(WpfColor.FromRgb(236, 243, 255));
        var majorBrush = new SolidColorBrush(WpfColor.FromRgb(220, 234, 255));
        minorBrush.Freeze();
        majorBrush.Freeze();

        for (var x = 0d; x <= MapCanvas.Width; x += gridSize)
        {
            MapCanvas.Children.Add(CreateGridLine(
                x,
                0,
                x,
                MapCanvas.Height,
                IsMajorGridLine(x) ? majorBrush : minorBrush));
        }

        for (var y = 0d; y <= MapCanvas.Height; y += gridSize)
        {
            MapCanvas.Children.Add(CreateGridLine(
                0,
                y,
                MapCanvas.Width,
                y,
                IsMajorGridLine(y) ? majorBrush : minorBrush));
        }
    }

    private static int GetGridLineCount(double width, double height, double gridSize)
    {
        if (width <= 0 || height <= 0 || gridSize <= 0)
        {
            return 0;
        }

        return (int)(Math.Floor(width / gridSize) + Math.Floor(height / gridSize) + 2);
    }

    private static bool IsMajorGridLine(double coordinate)
    {
        return Math.Abs(coordinate % MapMajorGridSize) < 0.001;
    }

    private static Line CreateGridLine(double x1, double y1, double x2, double y2, System.Windows.Media.Brush brush)
    {
        return new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = brush,
            StrokeThickness = 1,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
    }

    private void DrawDevice(FactoryMapDeviceViewNode device)
    {
        var deviceCode = GetDeviceCode(device);
        var content = new StackPanel
        {
            Orientation = WpfOrientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = WpfHorizontalAlignment.Center
        };
        content.Children.Add(new TextBlock
        {
            Text = device.Name,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(15, 23, 42)),
            FontFamily = new WpfFontFamily("Microsoft YaHei UI"),
            FontSize = 12,
            LineHeight = 15,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            FontWeight = FontWeights.SemiBold
        });

        if (!string.IsNullOrWhiteSpace(deviceCode))
        {
            content.Children.Add(new TextBlock
            {
                Text = deviceCode,
                Margin = new Thickness(0, 1, 0, 0),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.None,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(100, 116, 139)),
                FontFamily = new WpfFontFamily("Microsoft YaHei UI"),
                FontSize = 11,
                LineHeight = 14,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                FontWeight = FontWeights.Normal
            });
        }

        var border = new Border
        {
            Width = FactoryMapNodeGeometryService.GetWidth(device),
            Height = FactoryMapNodeGeometryService.GetHeight(device),
            Background = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(183, 198, 216)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = content,
            Padding = new Thickness(8, 4, 8, 4),
            Tag = device,
            ToolTip = string.IsNullOrWhiteSpace(deviceCode)
                ? $"{device.Name}\n{device.Key}"
                : $"{device.Name}\n{deviceCode}\n{device.Key}"
        };

        ApplyDeviceNormalVisual(border);

        border.MouseLeftButtonDown += Device_MouseLeftButtonDown;
        border.MouseLeftButtonUp += Device_MouseLeftButtonUp;
        border.PreviewMouseRightButtonDown += Device_PreviewMouseRightButtonDown;
        Canvas.SetLeft(border, device.X);
        Canvas.SetTop(border, device.Y);
        deviceByElement[border] = device;
        elementByDevice[device] = border;
        if (topologySelection.Contains(new FactoryMapObjectRef(FactoryMapObjectKind.Device, device.Id)))
        {
            ApplyDeviceSelectedVisual(border);
        }

        MapCanvas.Children.Add(border);
    }

    private void DrawConnector(FactoryMapConnectorViewNode connector)
    {
        var isSelected = ReferenceEquals(selectedConnector, connector);
        var size = isSelected ? SelectedConnectorSize : ConnectorSize;
        var connectorElement = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = WpfBrushes.White,
            Stroke = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235)),
            StrokeThickness = 2,
            Tag = connector,
            Cursor = WpfCursors.Hand,
            ToolTip = "连接点"
        };

        if (isSelected)
        {
            connectorElement.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = WpfColor.FromRgb(37, 99, 235),
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.28
            };
        }

        connectorElement.MouseLeftButtonDown += Connector_MouseLeftButtonDown;
        connectorElement.MouseMove += Connector_MouseMove;
        connectorElement.MouseLeftButtonUp += Connector_MouseLeftButtonUp;
        connectorElement.PreviewMouseRightButtonDown += Connector_PreviewMouseRightButtonDown;

        Canvas.SetLeft(connectorElement, connector.X - size / 2);
        Canvas.SetTop(connectorElement, connector.Y - size / 2);
        connectorByElement[connectorElement] = connector;
        elementByConnector[connector] = connectorElement;
        MapCanvas.Children.Add(connectorElement);
    }

    private void DrawEndpointPorts(FactoryMapEndpointViewData endpoint, string displayName)
    {
        if (!isEditMode)
        {
            return;
        }

        foreach (var port in new[]
        {
            FactoryMapPortKinds.Top,
            FactoryMapPortKinds.Right,
            FactoryMapPortKinds.Bottom,
            FactoryMapPortKinds.Left
        })
        {
            DrawEndpointPort(endpoint, port, displayName);
        }
    }

    private void DrawEndpointPort(FactoryMapEndpointViewData endpoint, string port, string displayName)
    {
        var isPending = pendingConnectionStart is not null
            && IsSameEndpoint(pendingConnectionStart.Endpoint, endpoint)
            && string.Equals(pendingConnectionStart.Port, port, StringComparison.OrdinalIgnoreCase);
        var size = endpoint.Device is not null ? 8.0 : 6.0;
        var point = FactoryMapEndpointGeometryService.GetPortPoint(endpoint, port);
        var portElement = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = isPending
                ? new SolidColorBrush(WpfColor.FromRgb(37, 99, 235))
                : WpfBrushes.White,
            Stroke = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235)),
            StrokeThickness = isPending ? 2.2 : 1.6,
            Cursor = WpfCursors.Cross,
            Tag = new EndpointPortPayload(endpoint, port, displayName),
            ToolTip = $"{displayName}：{GetPortDisplayName(port)}端口",
            SnapsToDevicePixels = true
        };
        portElement.MouseEnter += (_, _) =>
        {
            portElement.Fill = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235));
        };
        portElement.MouseLeave += (_, _) =>
        {
            if (!isPending)
            {
                portElement.Fill = WpfBrushes.White;
            }
        };
        portElement.MouseLeftButtonDown += EndpointPort_MouseLeftButtonDown;

        Canvas.SetLeft(portElement, point.X - size / 2);
        Canvas.SetTop(portElement, point.Y - size / 2);
        System.Windows.Controls.Panel.SetZIndex(portElement, 30);
        MapCanvas.Children.Add(portElement);
    }

    private static string GetPortDisplayName(string port)
    {
        return FactoryMapEndpointGeometryService.NormalizePort(port) switch
        {
            FactoryMapPortKinds.Top => "上",
            FactoryMapPortKinds.Right => "右",
            FactoryMapPortKinds.Bottom => "下",
            FactoryMapPortKinds.Left => "左",
            _ => "右"
        };
    }

    private static string GetDeviceCode(FactoryMapDeviceViewNode device)
    {
        var deviceCode = FactoryMapDeviceCodeParser.Parse(device.Shortcut?.TargetPath);
        return string.IsNullOrWhiteSpace(deviceCode)
            ? FactoryMapDeviceCodeParser.Parse(device.Key)
            : deviceCode;
    }

    private void Device_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: FactoryMapDeviceViewNode device } border)
        {
            return;
        }

        if (isEditMode)
        {
            SelectSingleDevice(device);
            var topologyMenu = CreateTopologyDeviceContextMenu(device);
            topologyMenu.PlacementTarget = border;
            topologyMenu.IsOpen = true;
            e.Handled = true;
            return;
        }

        SelectBrowseDevice(device);
        var menu = CreateDeviceContextMenu(device);
        menu.PlacementTarget = border;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu CreateTopologyDeviceContextMenu(FactoryMapDeviceViewNode device)
    {
        var menu = CreateTopologyContextMenu();
        var connectItem = new MenuItem { Header = "开始连接", MinWidth = 150 };
        ApplyModernMenuItemStyle(connectItem, isDanger: false);
        foreach (var side in FactoryMapPortKinds.All)
        {
            var sideItem = CreateTopologyMenuItem($"从{GetPortDisplayName(side)}侧连接", false, (_, _) =>
            {
                pendingTopologyPointId = FactoryMapLayoutTopologyConverter.CreateAttachedPointId(device.Id, side);
                RefreshMapModeStatus();
                RenderCurrentMap(resetView: false);
                SetStatusText($"已选择 {device.Name} {GetPortDisplayName(side)}侧连接点，请选择终点。");
            });
            connectItem.Items.Add(sideItem);
        }

        menu.Items.Add(connectItem);
        menu.Items.Add(CreateTopologyMenuItem("断开全部连接", true, (_, _) =>
        {
            if (dialogService.Confirm($"确定断开“{device.Name}”的全部连接吗？"))
            {
                ExecuteTopologyMutation(
                    () => topologyService.DisconnectNode(currentMap!, device.Id),
                    "节点的全部连接已断开。");
            }
        }));
        return menu;
    }

    private ContextMenu CreateDeviceContextMenu(FactoryMapDeviceViewNode device)
    {
        var menu = new ContextMenu
        {
            Padding = new Thickness(0),
            Background = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(209, 213, 219)),
            BorderThickness = new Thickness(1)
        };
        ApplyModernContextMenuStyle(menu);

        var capabilities = getContextMenuCapabilities(ContextMenuCapabilitySurfaces.FactoryMap);
        foreach (var capability in capabilities)
        {
            menu.Items.Add(CreateDeviceMenuItem(capability, device));
        }

        if (capabilities.Count > 0)
        {
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(CreateTopologyMenuItem("编辑", false, (_, _) => editShortcut(device.Shortcut)));
        menu.Items.Add(CreateTopologyMenuItem("删除", true, (_, _) => deleteShortcut(device.Shortcut)));

        return menu;
    }

    private MenuItem CreateDeviceMenuItem(
        ContextMenuCapabilityDefinition capability,
        FactoryMapDeviceViewNode device)
    {
        var item = new MenuItem
        {
            Header = capability.Name,
            MinWidth = 130,
            Padding = new Thickness(14, 8, 14, 8),
            Foreground = new SolidColorBrush(WpfColor.FromRgb(17, 24, 39)),
            Background = WpfBrushes.Transparent
        };
        ApplyModernMenuItemStyle(item, isDanger: false);

        item.Click += async (_, _) => await executeContextMenuCapability(device.Shortcut, capability);
        return item;
    }

    private static ControlTemplate CreateCompactContextMenuTemplate()
    {
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, WpfBrushes.White);
        borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Border.BorderBrushProperty));
        borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Border.BorderThicknessProperty));
        borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);

        var presenterFactory = new FrameworkElementFactory(typeof(ItemsPresenter));
        borderFactory.AppendChild(presenterFactory);

        return new ControlTemplate(typeof(ContextMenu))
        {
            VisualTree = borderFactory
        };
    }

    private static ControlTemplate CreateCompactMenuItemTemplate()
    {
        var rootFactory = new FrameworkElementFactory(typeof(Border), "Root");
        rootFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(System.Windows.Controls.Control.PaddingProperty));
        rootFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Control.BackgroundProperty));

        var presenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        presenterFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        presenterFactory.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        presenterFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        rootFactory.AppendChild(presenterFactory);

        var template = new ControlTemplate(typeof(MenuItem))
        {
            VisualTree = rootFactory
        };

        var highlightTrigger = new Trigger
        {
            Property = MenuItem.IsHighlightedProperty,
            Value = true
        };
        highlightTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(WpfColor.FromRgb(229, 241, 251)), "Root"));
        template.Triggers.Add(highlightTrigger);

        var disabledTrigger = new Trigger
        {
            Property = IsEnabledProperty,
            Value = false
        };
        disabledTrigger.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(WpfColor.FromRgb(156, 163, 175))));
        template.Triggers.Add(disabledTrigger);

        return template;
    }

    private static PointCollection CreateEdgePoints(FactoryMapDeviceEdgeViewData edge)
    {
        return CreateEdgePoints(edge, insetEndpoints: false);
    }

    internal static PointCollection CreateVisibleEdgePoints(FactoryMapDeviceEdgeViewData edge)
    {
        return CreateEdgePoints(edge, insetEndpoints: true);
    }

    internal static IReadOnlyList<FactoryMapVisibleEdgeSegment> CreateMergedVisibleEdgeSegments(
        IReadOnlyList<FactoryMapDeviceEdgeViewData> edges)
    {
        var segments = new List<NormalizedVisibleEdgeSegment>();
        foreach (var edge in edges)
        {
            var points = CreateVisibleEdgePoints(edge);
            for (var i = 0; i < points.Count - 1; i++)
            {
                if (TryCreateNormalizedVisibleEdgeSegment(points[i], points[i + 1], out var segment))
                {
                    segments.Add(segment);
                }
            }
        }

        return MergeVisibleEdgeSegments(segments);
    }

    private static bool TryCreateNormalizedVisibleEdgeSegment(
        WpfPoint start,
        WpfPoint end,
        out NormalizedVisibleEdgeSegment segment)
    {
        segment = new NormalizedVisibleEdgeSegment(true, 0, 0, 0);
        if (AreSamePoint(start, end))
        {
            return false;
        }

        if (AreSameCoordinate(start.Y, end.Y))
        {
            segment = new NormalizedVisibleEdgeSegment(
                true,
                QuantizeCoordinate(start.Y),
                Math.Min(start.X, end.X),
                Math.Max(start.X, end.X));
            return segment.To - segment.From > EdgeMergeTolerance;
        }

        if (AreSameCoordinate(start.X, end.X))
        {
            segment = new NormalizedVisibleEdgeSegment(
                false,
                QuantizeCoordinate(start.X),
                Math.Min(start.Y, end.Y),
                Math.Max(start.Y, end.Y));
            return segment.To - segment.From > EdgeMergeTolerance;
        }

        return false;
    }

    private static IReadOnlyList<FactoryMapVisibleEdgeSegment> MergeVisibleEdgeSegments(
        IReadOnlyList<NormalizedVisibleEdgeSegment> segments)
    {
        return segments
            .GroupBy(segment => (segment.IsHorizontal, segment.AxisKey))
            .OrderBy(group => group.Key.IsHorizontal ? 0 : 1)
            .ThenBy(group => group.Key.AxisKey)
            .SelectMany(MergeVisibleEdgeSegmentGroup)
            .ToList();
    }

    private static IEnumerable<FactoryMapVisibleEdgeSegment> MergeVisibleEdgeSegmentGroup(
        IGrouping<(bool IsHorizontal, long AxisKey), NormalizedVisibleEdgeSegment> group)
    {
        var ordered = group
            .OrderBy(segment => segment.From)
            .ThenBy(segment => segment.To)
            .ToList();
        if (ordered.Count == 0)
        {
            yield break;
        }

        var currentFrom = ordered[0].From;
        var currentTo = ordered[0].To;
        for (var i = 1; i < ordered.Count; i++)
        {
            var next = ordered[i];
            if (next.From <= currentTo + EdgeMergeTolerance)
            {
                currentTo = Math.Max(currentTo, next.To);
                continue;
            }

            yield return CreateVisibleEdgeSegment(group.Key.IsHorizontal, group.Key.AxisKey, currentFrom, currentTo);
            currentFrom = next.From;
            currentTo = next.To;
        }

        yield return CreateVisibleEdgeSegment(group.Key.IsHorizontal, group.Key.AxisKey, currentFrom, currentTo);
    }

    private static FactoryMapVisibleEdgeSegment CreateVisibleEdgeSegment(
        bool isHorizontal,
        long axisKey,
        double from,
        double to)
    {
        var axis = axisKey / EdgeMergePrecision;
        return isHorizontal
            ? new FactoryMapVisibleEdgeSegment(new WpfPoint(from, axis), new WpfPoint(to, axis))
            : new FactoryMapVisibleEdgeSegment(new WpfPoint(axis, from), new WpfPoint(axis, to));
    }

    private static long QuantizeCoordinate(double coordinate)
    {
        return (long)Math.Round(coordinate * EdgeMergePrecision, MidpointRounding.AwayFromZero);
    }

    private static bool AreSamePoint(WpfPoint first, WpfPoint second)
    {
        return AreSameCoordinate(first.X, second.X) && AreSameCoordinate(first.Y, second.Y);
    }

    private static bool AreSameCoordinate(double first, double second)
    {
        return Math.Abs(first - second) <= EdgeMergeTolerance;
    }

    private static PointCollection CreateEdgePoints(FactoryMapDeviceEdgeViewData edge, bool insetEndpoints)
    {
        var points = new PointCollection();
        var start = GetEdgeStart(edge);
        var end = GetEdgeEnd(edge);
        if (insetEndpoints)
        {
            start = InsetPortPoint(start, edge.FromPort);
            end = InsetPortPoint(end, edge.ToPort);
        }

        if (HasManualEdgePoints(edge))
        {
            var normalizedPoints = FactoryMapOrthogonalPathService.Normalize(start, edge.Points ?? [], end);
            points.Add(start);
            foreach (var point in normalizedPoints)
            {
                points.Add(new WpfPoint(point.X, point.Y));
            }

            points.Add(end);
            return points;
        }

        points.Add(start);
        foreach (var point in CreateDefaultOrthogonalMiddlePoints(start, edge.FromPort, end, edge.ToPort))
        {
            points.Add(point);
        }

        points.Add(end);
        return points;
    }

    private static WpfPoint InsetPortPoint(WpfPoint point, string port)
    {
        return FactoryMapEndpointGeometryService.NormalizePort(port) switch
        {
            FactoryMapPortKinds.Top => new WpfPoint(point.X, point.Y + EdgeEndpointInset),
            FactoryMapPortKinds.Right => new WpfPoint(point.X - EdgeEndpointInset, point.Y),
            FactoryMapPortKinds.Bottom => new WpfPoint(point.X, point.Y - EdgeEndpointInset),
            FactoryMapPortKinds.Left => new WpfPoint(point.X + EdgeEndpointInset, point.Y),
            _ => point
        };
    }

    private static IEnumerable<WpfPoint> CreateDefaultOrthogonalMiddlePoints(
        WpfPoint start,
        string fromPort,
        WpfPoint end,
        string toPort)
    {
        var normalizedFromPort = FactoryMapEndpointGeometryService.NormalizePort(fromPort);
        var normalizedToPort = FactoryMapEndpointGeometryService.NormalizePort(toPort, FactoryMapPortKinds.Left);
        var fromIsVertical = normalizedFromPort is FactoryMapPortKinds.Top or FactoryMapPortKinds.Bottom;
        var toIsVertical = normalizedToPort is FactoryMapPortKinds.Top or FactoryMapPortKinds.Bottom;

        if (fromIsVertical && toIsVertical)
        {
            var middleY = start.Y + (end.Y - start.Y) / 2;
            return [new WpfPoint(start.X, middleY), new WpfPoint(end.X, middleY)];
        }

        if (!fromIsVertical && !toIsVertical)
        {
            var middleX = start.X + (end.X - start.X) / 2;
            return [new WpfPoint(middleX, start.Y), new WpfPoint(middleX, end.Y)];
        }

        return fromIsVertical
            ? [new WpfPoint(start.X, end.Y)]
            : [new WpfPoint(end.X, start.Y)];
    }

    private static bool HasManualEdgePoints(FactoryMapDeviceEdgeViewData edge)
    {
        return edge.Points?.Any(point => double.IsFinite(point.X) && double.IsFinite(point.Y)) == true;
    }

    internal static bool IsValidEdgePointIndex(FactoryMapDeviceEdgeViewData edge, int pointIndex)
    {
        if (pointIndex < 0 || pointIndex >= edge.Points.Count)
        {
            return false;
        }

        var point = edge.Points[pointIndex];
        return double.IsFinite(point.X) && double.IsFinite(point.Y);
    }

    internal static double CalculatePointToSegmentDistanceSquared(
        WpfPoint point,
        WpfPoint segmentStart,
        WpfPoint segmentEnd)
    {
        var dx = segmentEnd.X - segmentStart.X;
        var dy = segmentEnd.Y - segmentStart.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= 0)
        {
            return CalculateDistanceSquared(point, segmentStart);
        }

        var t = (((point.X - segmentStart.X) * dx) + ((point.Y - segmentStart.Y) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        var projected = new WpfPoint(segmentStart.X + (t * dx), segmentStart.Y + (t * dy));
        return CalculateDistanceSquared(point, projected);
    }

    internal static int GetInsertPointIndex(FactoryMapDeviceEdgeViewData edge, WpfPoint clickPoint)
    {
        var path = GetEditableEdgePoints(edge);
        if (path.Count < 2)
        {
            return 0;
        }

        var bestIndex = 0;
        var bestDistance = double.PositiveInfinity;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var distance = CalculatePointToSegmentDistanceSquared(clickPoint, path[i], path[i + 1]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return Math.Clamp(bestIndex, 0, Math.Max(0, path.Count - 2));
    }

    internal static FactoryMapPoint SnapEdgePointToGrid(WpfPoint point)
    {
        return new FactoryMapPoint
        {
            X = FactoryMapEditMath.ClampAndSnapToGrid(point.X, SnapGridSize),
            Y = FactoryMapEditMath.ClampAndSnapToGrid(point.Y, SnapGridSize)
        };
    }

    private static List<WpfPoint> GetEditableEdgePoints(FactoryMapDeviceEdgeViewData edge)
    {
        return GetEditableEdgePath(edge);
    }

    private static List<WpfPoint> GetEditableEdgePath(FactoryMapDeviceEdgeViewData edge)
    {
        var points = new List<WpfPoint>
        {
            GetEdgeStart(edge)
        };

        if (HasManualEdgePoints(edge))
        {
            var normalizedPoints = FactoryMapOrthogonalPathService.Normalize(
                GetEdgeStart(edge),
                edge.Points ?? [],
                GetEdgeEnd(edge));
            foreach (var point in normalizedPoints)
            {
                points.Add(new WpfPoint(point.X, point.Y));
            }

            points.Add(GetEdgeEnd(edge));
            return points;
        }

        var start = GetEdgeStart(edge);
        var end = GetEdgeEnd(edge);
        points.AddRange(CreateDefaultOrthogonalMiddlePoints(start, edge.FromPort, end, edge.ToPort));
        points.Add(GetEdgeEnd(edge));
        return points;
    }

    private static List<FactoryMapPoint> GetServiceEdgePoints(FactoryMapDeviceEdgeViewData edge)
    {
        if (HasManualEdgePoints(edge))
        {
            return edge.Points ?? [];
        }

        var path = GetEditableEdgePath(edge);
        return path
            .Skip(1)
            .Take(Math.Max(0, path.Count - 2))
            .Select(point => new FactoryMapPoint { X = point.X, Y = point.Y })
            .ToList();
    }

    private static int FindNearestEditableSegmentIndex(FactoryMapDeviceEdgeViewData edge, WpfPoint clickPoint)
    {
        return FactoryMapOrthogonalPathService.FindNearestSegmentIndex(
            GetEdgeStart(edge),
            GetServiceEdgePoints(edge),
            GetEdgeEnd(edge),
            clickPoint);
    }

    private static bool IsEditableSegmentDraggable(FactoryMapDeviceEdgeViewData edge, int segmentIndex)
    {
        var path = GetEditableEdgePath(edge);
        return segmentIndex > 0 && segmentIndex < path.Count - 2;
    }

    private static WpfCursor GetSegmentCursor(FactoryMapDeviceEdgeViewData edge, int segmentIndex)
    {
        var path = GetEditableEdgePath(edge);
        if (segmentIndex < 0 || segmentIndex >= path.Count - 1)
        {
            return WpfCursors.Arrow;
        }

        var start = path[segmentIndex];
        var end = path[segmentIndex + 1];
        return Math.Abs(start.Y - end.Y) < 0.001
            ? WpfCursors.SizeNS
            : WpfCursors.SizeWE;
    }

    private static double CalculateDistanceSquared(WpfPoint first, WpfPoint second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return (dx * dx) + (dy * dy);
    }

    private void TopologyPoint_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode
            || currentMap is null
            || sender is not FrameworkElement { Tag: string pointId } element)
        {
            return;
        }

        var point = currentMap.ConnectionPoints.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, pointId, StringComparison.OrdinalIgnoreCase));
        if (point is null)
        {
            return;
        }

        if (!FlushPendingTopologySave())
        {
            e.Handled = true;
            return;
        }

        var objectRef = new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, point.Id);
        if (point.Kind == FactoryMapConnectionPointKinds.Free
            && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            pendingTopologyPointId = null;
            topologySelection.Toggle(objectRef);
            RefreshSelectionVisuals();
            SetStatusText($"已选中 {topologySelection.SelectedObjects.Count} 个对象。");
            e.Handled = true;
            return;
        }

        if (HasTopologyConnectionDraft)
        {
            HandleTopologyPointConnection(point.Id);
            e.Handled = true;
            return;
        }

        if (point.Kind == FactoryMapConnectionPointKinds.Free
            && topologySelection.Contains(objectRef)
            && topologySelection.SelectedObjects.Count > 1)
        {
            BeginSelectedObjectsDrag(e);
            e.Handled = true;
            return;
        }

        topologySelection.Select(objectRef);
        ClearEdgeSelection();
        if (point.Kind == FactoryMapConnectionPointKinds.Attached)
        {
            pendingTopologyPointId = point.Id;
            RenderCurrentMap(resetView: false);
            RefreshMapModeStatus();
            SetStatusText($"已选择{GetTopologyPointDisplayName(point)}，请选择连接终点。");
            e.Handled = true;
            return;
        }

        if (point.Kind == FactoryMapConnectionPointKinds.Junction
            && FactoryMapJunctionAxes.Normalize(point.JunctionAxis) == FactoryMapJunctionAxes.Locked)
        {
            pendingTopologyPointId = point.Id;
            RenderCurrentMap(resetView: false);
            RefreshMapModeStatus();
            SetStatusText("该分支连接点方向已锁定，可作为连接起点；如需移动请拖动相邻线段。");
            e.Handled = true;
            return;
        }

        activeTopologyPointId = point.Id;
        activeTopologyPointElement = element;
        topologyDragStartMapPoint = e.GetPosition(MapCanvas);
        topologyPointStartX = point.X;
        topologyPointStartY = point.Y;
        isDraggingTopologyPoint = true;
        interactionState.Begin(FactoryMapInteractionKind.DraggingObject);
        MapViewport.CaptureMouse();
        MapViewport.Cursor = GetTopologyPointCursor(point);
        SetStatusText($"已选择{GetTopologyPointDisplayName(point)}，可拖动或使用方向键移动。");
        e.Handled = true;
    }

    private void TopologyPoint_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode
            || currentMap is null
            || sender is not FrameworkElement { Tag: string pointId } element)
        {
            return;
        }

        var point = currentMap.ConnectionPoints.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, pointId, StringComparison.OrdinalIgnoreCase));
        if (point is null)
        {
            return;
        }

        topologySelection.Select(new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, point.Id));
        var menu = CreateTopologyPointContextMenu(point);
        menu.PlacementTarget = element;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu CreateTopologyPointContextMenu(FactoryMapConnectionPoint point)
    {
        var menu = CreateTopologyContextMenu();
        if (point.Kind != FactoryMapConnectionPointKinds.Bend)
        {
            menu.Items.Add(CreateTopologyMenuItem("开始连接", false, (_, _) =>
            {
                pendingTopologyPointId = point.Id;
                RefreshMapModeStatus();
                RenderCurrentMap(resetView: false);
                SetStatusText($"已选择起点：{GetTopologyPointDisplayName(point)}，请选择终点连接点。");
            }));
        }

        if (topologyService.GetDegree(currentMap!, point.Id) > 0)
        {
            menu.Items.Add(CreateTopologyMenuItem("断开全部", false, (_, _) =>
                ExecuteTopologyMutation(
                    () => topologyService.DisconnectPoint(currentMap!, point.Id),
                    "连接点已断开。")));
        }

        if (point.Kind == FactoryMapConnectionPointKinds.Bend)
        {
            menu.Items.Add(CreateTopologyMenuItem("转换为普通连接点", false, (_, _) =>
                ExecuteTopologyMutation(
                    () => topologyService.PromoteBendToFree(currentMap!, point.Id),
                    "折弯点已转换为普通连接点。")));
            menu.Items.Add(CreateTopologyMenuItem("删除折弯点", true, (_, _) =>
                DeleteTopologyPoint(point)));
        }
        else if (point.Kind == FactoryMapConnectionPointKinds.Junction)
        {
            menu.Items.Add(CreateTopologyMenuItem("转换为普通连接点", false, (_, _) =>
            {
                if (dialogService.Confirm("转换后该连接点可以自由移动，是否继续？"))
                {
                    ExecuteTopologyMutation(
                        () => topologyService.ConvertJunctionToFree(currentMap!, point.Id),
                        "分支连接点已转换为普通连接点。");
                }
            }));
            menu.Items.Add(CreateTopologyMenuItem("删除分支连接点", true, (_, _) =>
                DeleteTopologyPoint(point)));
        }
        else if (point.Kind == FactoryMapConnectionPointKinds.Free)
        {
            menu.Items.Add(CreateTopologyMenuItem("删除连接点", true, (_, _) =>
                DeleteTopologyPoint(point)));
        }

        return menu;
    }

    private void DeleteTopologyPoint(FactoryMapConnectionPoint point)
    {
        if (currentMap is null)
        {
            return;
        }

        var degree = topologyService.GetDegree(currentMap, point.Id);
        if (degree > 0
            && !dialogService.Confirm($"删除该连接点会同时删除 {degree} 条关联线段，是否继续？"))
        {
            return;
        }

        ExecuteTopologyMutation(
            () => topologyService.DeleteConnectionPoint(currentMap, point.Id),
            "连接点及其关联线段已删除。");
    }

    private void TopologySegment_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode
            || currentMap is null
            || sender is not FrameworkElement { Tag: FactoryMapVisibleSegment visibleSegment })
        {
            return;
        }

        var segmentId = visibleSegment.TopSegmentId;
        if (!FlushPendingTopologySave())
        {
            e.Handled = true;
            return;
        }
        if (HasTopologyConnectionDraft)
        {
            CompleteConnectionAtTopologySegment(segmentId, e.GetPosition(MapCanvas));
            e.Handled = true;
            return;
        }

        topologySelection.Select(new FactoryMapObjectRef(FactoryMapObjectKind.Segment, segmentId));
        ClearEdgeSelection();
        activeTopologySegmentId = segmentId;
        topologyDragStartMapPoint = e.GetPosition(MapCanvas);
        isDraggingTopologySegment = true;
        interactionState.Begin(FactoryMapInteractionKind.DraggingObject);
        MapViewport.CaptureMouse();
        MapViewport.Cursor = Math.Abs(visibleSegment.Start.Y - visibleSegment.End.Y) < 0.001
            ? WpfCursors.SizeNS
            : WpfCursors.SizeWE;
        RenderCurrentMap(resetView: false);
        SetStatusText("已选择线段，可拖动通道或使用方向键移动。");
        e.Handled = true;
    }

    private void TopologySegment_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode
            || sender is not FrameworkElement { Tag: FactoryMapVisibleSegment visibleSegment } element)
        {
            return;
        }

        var clickPoint = e.GetPosition(MapCanvas);
        topologySelection.Select(new FactoryMapObjectRef(
            FactoryMapObjectKind.Segment,
            visibleSegment.TopSegmentId));
        var menu = CreateTopologySegmentContextMenu(visibleSegment, clickPoint);
        menu.PlacementTarget = element;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu CreateTopologySegmentContextMenu(
        FactoryMapVisibleSegment visibleSegment,
        WpfPoint clickPoint)
    {
        var menu = CreateTopologyContextMenu();
        if (visibleSegment.SourceSegmentIds.Count <= 1)
        {
            AddTopologySegmentMenuItems(menu.Items, visibleSegment.TopSegmentId, clickPoint);
            return menu;
        }

        var headers = CreateTopologySegmentMenuHeaders(currentMap!, visibleSegment);
        for (var index = 0; index < visibleSegment.SourceSegmentIds.Count; index++)
        {
            var segmentId = visibleSegment.SourceSegmentIds[index];
            var group = new MenuItem
            {
                Header = headers[index],
                MinWidth = 220,
                Tag = segmentId
            };
            ApplyModernMenuItemStyle(group, isDanger: false);
            group.AddHandler(
                Mouse.PreviewMouseUpEvent,
                new MouseButtonEventHandler(TopologySegmentMenuGroup_PreviewMouseLeftButtonUp),
                handledEventsToo: true);
            AddTopologySegmentMenuItems(group.Items, segmentId, clickPoint);
            menu.Items.Add(group);
        }

        return menu;
    }

    internal static IReadOnlyList<string> CreateTopologySegmentMenuHeaders(
        FactoryMapDeviceViewData map,
        FactoryMapVisibleSegment visibleSegment)
    {
        var enumeration = new FactoryMapLogicalRouteService().Enumerate(map);
        var routeBySegmentId = enumeration.Success
            ? enumeration.Routes
                .SelectMany(route => route.SegmentIds.Select(segmentId => (segmentId, route)))
                .GroupBy(item => item.segmentId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().route, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, FactoryMapLogicalRoute>(StringComparer.OrdinalIgnoreCase);

        var headers = new List<string>(visibleSegment.SourceSegmentIds.Count);
        for (var index = 0; index < visibleSegment.SourceSegmentIds.Count; index++)
        {
            var segmentId = visibleSegment.SourceSegmentIds[index];
            var routeDescription = routeBySegmentId.TryGetValue(segmentId, out var route)
                ? $"{GetTopologyRouteEndpointDisplayName(map, route.StartPointId)} → {GetTopologyRouteEndpointDisplayName(map, route.EndPointId)}"
                : "未识别线路";
            var topSuffix = string.Equals(segmentId, visibleSegment.TopSegmentId, StringComparison.OrdinalIgnoreCase)
                ? "（当前顶层）"
                : string.Empty;
            headers.Add($"线路 {index + 1}：{routeDescription}{topSuffix}");
        }

        return headers;
    }

    internal static bool TrySelectTopologySegmentForEditing(
        FactoryMapDeviceViewData map,
        FactoryMapSelectionState selection,
        string segmentId)
    {
        if (string.IsNullOrWhiteSpace(segmentId)
            || !map.Segments.Any(segment =>
                string.Equals(segment.Id, segmentId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        selection.Select(new FactoryMapObjectRef(FactoryMapObjectKind.Segment, segmentId));
        return true;
    }

    private void TopologySegmentMenuGroup_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || sender is not MenuItem { Tag: string segmentId } group
            || !ReferenceEquals(FindNearestMenuItem(e.OriginalSource as DependencyObject), group))
        {
            return;
        }

        if (ItemsControl.ItemsControlFromItemContainer(group) is ContextMenu menu)
        {
            menu.IsOpen = false;
        }

        e.Handled = true;
        SelectTopologySegmentFromContextMenu(segmentId);
    }

    private void SelectTopologySegmentFromContextMenu(string segmentId)
    {
        if (currentMap is null
            || !TrySelectTopologySegmentForEditing(currentMap, topologySelection, segmentId))
        {
            SetStatusText("所选线路已不存在，无法进入编辑状态。");
            return;
        }

        ClearEdgeSelection();
        activeTopologySegmentId = null;
        RenderCurrentMap(resetView: false);
        SetStatusText("已选中线路，可使用鼠标拖动或方向键移动。");
        RestoreMapFocusAfterToolbarClick();
    }

    private static MenuItem? FindNearestMenuItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is MenuItem menuItem)
            {
                return menuItem;
            }

            try
            {
                source = VisualTreeHelper.GetParent(source);
            }
            catch (InvalidOperationException)
            {
                source = LogicalTreeHelper.GetParent(source);
            }
        }

        return null;
    }

    private static string GetTopologyRouteEndpointDisplayName(
        FactoryMapDeviceViewData map,
        string pointId)
    {
        var point = map.ConnectionPoints.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, pointId, StringComparison.OrdinalIgnoreCase));
        if (point is null)
        {
            return "未知连接点";
        }

        if (point.Kind == FactoryMapConnectionPointKinds.Attached)
        {
            var device = map.Devices.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, point.OwnerNodeId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(device?.Name))
            {
                return device.Name;
            }

            if (!string.IsNullOrWhiteSpace(device?.Key))
            {
                return device.Key;
            }

            return "节点连接点";
        }

        var coordinates = $"{point.X:0.#}, {point.Y:0.#}";
        return point.Kind switch
        {
            FactoryMapConnectionPointKinds.Junction => $"分支点（{coordinates}）",
            FactoryMapConnectionPointKinds.Free => $"连接点（{coordinates}）",
            _ => $"连接点（{coordinates}）"
        };
    }

    private void AddTopologySegmentMenuItems(ItemCollection items, string segmentId, WpfPoint clickPoint)
    {
        items.Add(CreateTopologyMenuItem("从此处建立分支", false, (_, _) =>
            StartConnectionFromTopologySegment(segmentId, clickPoint)));
        items.Add(CreateTopologyMenuItem("断开/删除线段", true, (_, _) =>
            ExecuteTopologyMutation(
                () => topologyService.DisconnectSegment(currentMap!, segmentId),
                "线段已断开。")));
    }

    private void StartConnectionFromTopologySegment(string segmentId, WpfPoint clickPoint)
    {
        if (!FlushPendingTopologySave())
        {
            return;
        }

        if (!TryProjectTopologySegmentPoint(segmentId, clickPoint, out var projected)
            || !interactionState.BeginSegmentConnectionDraft(segmentId, projected.X, projected.Y))
        {
            SetStatusText("当前无法从该线段建立分支。");
            return;
        }

        topologySelection.Select(new FactoryMapObjectRef(FactoryMapObjectKind.Segment, segmentId));
        RefreshMapModeStatus();
        RenderCurrentMap(resetView: false);
        SetStatusText("请选择分支连接终点：连接点或另一条线段。");
    }

    private bool TryProjectTopologySegmentPoint(string segmentId, WpfPoint clickPoint, out WpfPoint projected)
    {
        projected = default;
        var segment = currentMap?.Segments.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, segmentId, StringComparison.OrdinalIgnoreCase));
        var from = segment is null ? null : FindConnectionPoint(segment.FromPointId);
        var to = segment is null ? null : FindConnectionPoint(segment.ToPointId);
        if (from is null || to is null)
        {
            return false;
        }

        if (Math.Abs(from.Y - to.Y) < 0.001)
        {
            projected = new WpfPoint(
                Math.Clamp(SnapToGrid(clickPoint.X), Math.Min(from.X, to.X), Math.Max(from.X, to.X)),
                from.Y);
            return true;
        }

        if (Math.Abs(from.X - to.X) < 0.001)
        {
            projected = new WpfPoint(
                from.X,
                Math.Clamp(SnapToGrid(clickPoint.Y), Math.Min(from.Y, to.Y), Math.Max(from.Y, to.Y)));
            return true;
        }

        return false;
    }

    private static double SnapToGrid(double value)
    {
        return Math.Round(value / SnapGridSize, MidpointRounding.AwayFromZero) * SnapGridSize;
    }

    private void CompleteConnectionAtTopologySegment(string segmentId, WpfPoint clickPoint)
    {
        var draft = interactionState.ConnectionDraft;
        if (currentMap is null || draft is null)
        {
            return;
        }

        var snapshot = CaptureTopologySnapshot(currentMap);
        var result = connectionDraftService.CompleteToSegment(
            currentMap,
            draft,
            segmentId,
            clickPoint.X,
            clickPoint.Y,
            SnapGridSize,
            SnapGridSize);
        if (!result.Success)
        {
            SetStatusText(result.ErrorMessage ?? "连接失败，请重新选择连接终点。");
            return;
        }

        interactionState.CancelConnectionDraft();
        if (!string.IsNullOrWhiteSpace(result.PointId))
        {
            topologySelection.Select(new FactoryMapObjectRef(
                FactoryMapObjectKind.ConnectionPoint,
                result.PointId));
        }
        if (!TrySaveTopologyChange(snapshot, "连接到线段后保存失败。"))
        {
            return;
        }

        RenderCurrentMap(resetView: false);
        SetStatusText("连接已创建。");
    }

    private void HandleTopologyPointConnection(string pointId)
    {
        if (currentMap is null)
        {
            return;
        }

        var targetPoint = FindConnectionPoint(pointId);
        if (targetPoint is null)
        {
            SetStatusText("连接终点不存在，请重新选择。");
            return;
        }

        if (targetPoint.Kind == FactoryMapConnectionPointKinds.Bend)
        {
            SetStatusText("折弯点不能直接作为连接终点，请先转换为普通连接点。");
            return;
        }

        var draft = interactionState.ConnectionDraft;
        if (draft is null)
        {
            pendingTopologyPointId = pointId;
            RenderCurrentMap(resetView: false);
            SetStatusText("已选择连接起点，请选择终点连接点。");
            return;
        }

        if (draft.OriginKind == FactoryMapConnectionOriginKinds.Point
            && string.Equals(draft.PointId, pointId, StringComparison.OrdinalIgnoreCase))
        {
            pendingTopologyPointId = null;
            RenderCurrentMap(resetView: false);
            SetStatusText("已取消连接起点。");
            return;
        }

        var snapshot = CaptureTopologySnapshot(currentMap);
        var result = connectionDraftService.CompleteToPoint(
            currentMap,
            draft,
            pointId,
            SnapGridSize,
            SnapGridSize);
        if (!result.Success)
        {
            SetStatusText(result.ErrorMessage ?? "连接失败。");
            return;
        }

        interactionState.CancelConnectionDraft();
        if (!TrySaveTopologyChange(snapshot, "连接创建后保存失败。"))
        {
            return;
        }

        RenderCurrentMap(resetView: false);
        SetStatusText("连接已创建，可继续选择新的连接起点。");
    }

    private ContextMenu CreateTopologyContextMenu()
    {
        var menu = new ContextMenu
        {
            Padding = new Thickness(0),
            Background = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(209, 213, 219)),
            BorderThickness = new Thickness(1)
        };
        ApplyModernContextMenuStyle(menu);
        return menu;
    }

    private static MenuItem CreateTopologyMenuItem(
        string header,
        bool isDanger,
        RoutedEventHandler click)
    {
        var item = CreateEdgeContextMenuItem(header, header, isDanger);
        item.Click += click;
        return item;
    }

    private void ExecuteTopologyMutation(
        Func<FactoryMapTopologyOperationResult> mutation,
        string successMessage)
    {
        if (currentMap is null)
        {
            return;
        }

        var snapshot = CaptureTopologySnapshot(currentMap);
        var result = mutation();
        if (!result.Success)
        {
            SetStatusText(result.ErrorMessage ?? "地图拓扑操作失败。");
            return;
        }

        if (!TrySaveTopologyChange(snapshot, "地图拓扑修改后保存失败。"))
        {
            return;
        }

        topologySelection.Clear();
        pendingTopologyPointId = null;
        RenderCurrentMap(resetView: false);
        SetStatusText(successMessage);
    }

    private bool TrySaveTopologyChange(TopologySnapshot snapshot, string errorMessage)
    {
        if (currentMap is not null && saveLayout(currentMap))
        {
            CancelPendingTopologySave();
            return true;
        }

        if (currentMap is not null)
        {
            RestoreTopologySnapshot(currentMap, snapshot);
            RenderCurrentMap(resetView: false);
        }

        dialogService.ShowError(errorMessage);
        return false;
    }

    private void ScheduleTopologySave(TopologySnapshot snapshot, string errorMessage)
    {
        pendingTopologySaveSnapshot ??= snapshot;
        pendingTopologySaveErrorMessage = errorMessage;
        topologySaveTimer.Stop();
        topologySaveTimer.Start();
    }

    private bool FlushPendingTopologySave()
    {
        topologySaveTimer.Stop();
        if (pendingTopologySaveSnapshot is null || currentMap is null)
        {
            pendingTopologySaveSnapshot = null;
            return true;
        }

        var snapshot = pendingTopologySaveSnapshot;
        var errorMessage = pendingTopologySaveErrorMessage;
        pendingTopologySaveSnapshot = null;
        if (saveLayout(currentMap))
        {
            return true;
        }

        RestoreTopologySnapshot(currentMap, snapshot);
        RenderCurrentMap(resetView: false);
        dialogService.ShowError(errorMessage);
        return false;
    }

    private void CancelPendingTopologySave()
    {
        topologySaveTimer.Stop();
        pendingTopologySaveSnapshot = null;
    }

    private static void RestoreTopologySnapshot(
        FactoryMapDeviceViewData map,
        TopologySnapshot snapshot)
    {
        map.ConnectionPoints = snapshot.Points;
        map.Segments = snapshot.Segments;
        foreach (var device in map.Devices)
        {
            if (snapshot.DevicePositions.TryGetValue(device.Id, out var position))
            {
                device.X = position.X;
                device.Y = position.Y;
            }
        }
    }

    private static TopologySnapshot CaptureTopologySnapshot(FactoryMapDeviceViewData map)
    {
        return new TopologySnapshot(
            map.ConnectionPoints.Select(point => new FactoryMapConnectionPoint
            {
                Id = point.Id,
                Kind = point.Kind,
                OwnerNodeId = point.OwnerNodeId,
                Side = point.Side,
                JunctionAxis = point.JunctionAxis,
                X = point.X,
                Y = point.Y
            }).ToList(),
            map.Segments.Select(segment => new FactoryMapSegment
            {
                Id = segment.Id,
                FromPointId = segment.FromPointId,
                ToPointId = segment.ToPointId,
                ZIndex = segment.ZIndex
            }).ToList(),
            map.Devices.ToDictionary(
                device => device.Id,
                device => new WpfPoint(device.X, device.Y),
                StringComparer.OrdinalIgnoreCase));
    }

    private ContextMenu CreateEdgeContextMenu(FactoryMapDeviceEdgeViewData edge, WpfPoint clickPoint)
    {
        var payload = new EdgeContextMenuPayload(edge, clickPoint, FindNearestEditableSegmentIndex(edge, clickPoint));

        var addItem = CreateEdgeContextMenuItem("插入分支点", payload, isDanger: false);
        addItem.Click += AddConnectorOnEdge_Click;

        var deleteItem = CreateEdgeContextMenuItem("删除连线", edge, isDanger: true);
        deleteItem.Click += DeleteEdge_Click;

        var menu = new ContextMenu
        {
            Padding = new Thickness(0),
            Background = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(209, 213, 219)),
            BorderThickness = new Thickness(1)
        };
        ApplyModernContextMenuStyle(menu);
        menu.Items.Add(addItem);
        menu.Items.Add(deleteItem);
        return menu;
    }

    private static MenuItem CreateEdgeContextMenuItem(string header, object tag, bool isDanger)
    {
        var item = new MenuItem
        {
            Header = header,
            Tag = tag,
            MinWidth = 130,
            Padding = new Thickness(14, 8, 14, 8),
            Foreground = new SolidColorBrush(WpfColor.FromRgb(17, 24, 39)),
            Background = WpfBrushes.Transparent,
        };
        ApplyModernMenuItemStyle(item, isDanger);
        return item;
    }

    private void Edge_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode)
        {
            e.Handled = true;
            return;
        }

        if (sender is not Polyline { Tag: FactoryMapDeviceEdgeViewData edge } polyline)
        {
            return;
        }

        var clickPoint = e.GetPosition(MapCanvas);
        selectedEdge = edge;
        selectedSegmentEdge = edge;
        selectedSegmentIndex = FindNearestEditableSegmentIndex(edge, clickPoint);
        var menu = CreateEdgeContextMenu(edge, clickPoint);
        menu.PlacementTarget = polyline;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void Edge_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        if (sender is not Polyline { Tag: FactoryMapDeviceEdgeViewData edge })
        {
            return;
        }

        selectedEdge = edge;
        selectedSegmentEdge = edge;
        selectedSegmentIndex = FindNearestEditableSegmentIndex(edge, e.GetPosition(MapCanvas));
        ClearPendingConnectionStart();
        ClearSelectedDeviceSelection();
        if (selectedSegmentIndex >= 0 && IsEditableSegmentDraggable(edge, selectedSegmentIndex))
        {
            activeSegmentEdge = edge;
            activeSegmentIndex = selectedSegmentIndex;
            isDraggingEdgeSegment = true;
            MapViewport.CaptureMouse();
            MapViewport.Cursor = GetSegmentCursor(edge, selectedSegmentIndex);
        }

        RenderCurrentMap(resetView: false);
        SetStatusText(IsEditableSegmentDraggable(edge, selectedSegmentIndex)
            ? "已选中连线通道，可拖动高亮线段调整路径。"
            : "已选中端点连接段，可右键添加绕行段后再调整。");
        e.Handled = true;
    }

    private void EdgePointHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        if (sender is not FrameworkElement { Tag: EdgePointHandlePayload payload }
            || !IsValidEdgePointIndex(payload.Edge, payload.PointIndex))
        {
            return;
        }

        selectedEdge = payload.Edge;
        selectedSegmentEdge = null;
        selectedSegmentIndex = -1;
        activeEdgePointEdge = payload.Edge;
        activeEdgePointIndex = payload.PointIndex;
        isDraggingEdgePoint = true;
        ClearEdgeSegmentDragState();
        ClearPendingConnectionStart();
        MapViewport.CaptureMouse();
        MapViewport.Cursor = WpfCursors.SizeAll;
        e.Handled = true;
    }

    private void EdgePointHandle_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!isDraggingEdgePoint)
        {
            return;
        }

        HandleEdgePointDragMove(e);
    }

    private void EdgePointHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isDraggingEdgePoint)
        {
            return;
        }

        EndEdgePointDrag(save: true);
        e.Handled = true;
    }

    private void EdgePointHandle_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        if (sender is not FrameworkElement { Tag: EdgePointHandlePayload payload }
            || !IsValidEdgePointIndex(payload.Edge, payload.PointIndex))
        {
            return;
        }

        EndEdgePointDrag(save: false);
        selectedEdge = payload.Edge;
        selectedSegmentEdge = null;
        selectedSegmentIndex = -1;
        var menu = CreateEdgePointContextMenu(payload.Edge, payload.PointIndex);
        menu.PlacementTarget = (FrameworkElement)sender;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static void ApplyModernContextMenuStyle(ContextMenu menu)
    {
        ContextMenuInputBehavior.SetSuppressRightClickActivation(menu, true);

        if (System.Windows.Application.Current.TryFindResource("ModernContextMenuStyle") is Style style)
        {
            menu.Style = style;
        }
        else
        {
            menu.Template = CreateCompactContextMenuTemplate();
        }
    }

    private static void ApplyModernMenuItemStyle(MenuItem item, bool isDanger)
    {
        var styleKey = isDanger ? "ModernDangerMenuItemStyle" : "ModernMenuItemStyle";
        if (System.Windows.Application.Current.TryFindResource(styleKey) is Style style)
        {
            item.Style = style;
        }
        else
        {
            item.Template = CreateCompactMenuItemTemplate();
        }
    }

    private void AddEdgeDetour_Click(object sender, RoutedEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        if (sender is not MenuItem { Tag: EdgeContextMenuPayload payload } || currentMap is null)
        {
            return;
        }

        var edge = payload.Edge;
        edge.Points = FactoryMapOrthogonalPathService.InsertDetourOnSegment(
            GetEdgeStart(edge),
            GetServiceEdgePoints(edge),
            GetEdgeEnd(edge),
            payload.SegmentIndex,
            payload.ClickPoint,
            SnapGridSize);

        selectedEdge = edge;
        selectedSegmentEdge = edge;
        selectedSegmentIndex = payload.SegmentIndex;
        ClearPendingConnectionStart();
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("绕行段已添加，但地图布局保存失败。");
            return;
        }

        SetStatusText("绕行段已添加，可拖动高亮线段调整路径。");
    }

    private void AddConnectorOnEdge_Click(object sender, RoutedEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        if (sender is not MenuItem { Tag: EdgeContextMenuPayload payload } || currentMap is null)
        {
            return;
        }

        var edge = payload.Edge;
        if (!currentMap.Edges.Contains(edge))
        {
            return;
        }

        var connector = new FactoryMapConnectorViewNode
        {
            Id = CreateConnectorId(currentMap),
            X = FactoryMapEditMath.ClampAndSnapToGrid(payload.ClickPoint.X, SnapGridSize),
            Y = FactoryMapEditMath.ClampAndSnapToGrid(payload.ClickPoint.Y, SnapGridSize)
        };
        currentMap.Connectors.Add(connector);
        currentMap.Edges.Remove(edge);
        var connectorEndpoint = FactoryMapEndpointViewData.FromConnector(connector);
        currentMap.Edges.Add(new FactoryMapDeviceEdgeViewData
        {
            From = edge.From,
            FromPort = edge.FromPort,
            To = connectorEndpoint,
            ToPort = FactoryMapEndpointGeometryService.InferIncomingPort(edge.From, connectorEndpoint)
        });
        currentMap.Edges.Add(new FactoryMapDeviceEdgeViewData
        {
            From = connectorEndpoint,
            FromPort = FactoryMapEndpointGeometryService.InferOutgoingPort(connectorEndpoint, edge.To),
            To = edge.To,
            ToPort = edge.ToPort
        });

        selectedConnector = connector;
        ClearEdgeSelection();
        ClearSelectedDeviceSelection();
        ClearPendingConnectionStart();
        RefreshMapModeStatus();
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("分支点已插入，但地图布局保存失败。");
            return;
        }

        SetStatusText("已插入分支点，请点击它的端口继续连接其他节点。");
    }

    private void ClearEdgePoints_Click(object sender, RoutedEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        if (sender is not MenuItem { Tag: FactoryMapDeviceEdgeViewData edge } || currentMap is null)
        {
            return;
        }

        edge.Points.Clear();
        selectedEdge = edge;
        selectedSegmentEdge = null;
        selectedSegmentIndex = -1;
        ClearPendingConnectionStart();
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("折线已清除，但地图布局保存失败。");
            return;
        }

        SetStatusText("全部折线已清除，连线已恢复自动路径。");
    }

    private void HandleEdgePointDragMove(WpfMouseEventArgs e)
    {
        if (!isDraggingEdgePoint)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndEdgePointDrag(save: true);
            e.Handled = true;
            return;
        }

        var mapPoint = ViewportPointToMapPoint(e.GetPosition(MapViewport));
        UpdateActiveEdgePointPosition(mapPoint);
        RenderCurrentMap(resetView: false);
        e.Handled = true;
    }

    private void HandleEdgeSegmentDragMove(WpfMouseEventArgs e)
    {
        if (!isDraggingEdgeSegment)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndEdgeSegmentDrag(save: true);
            e.Handled = true;
            return;
        }

        var mapPoint = ViewportPointToMapPoint(e.GetPosition(MapViewport));
        UpdateActiveEdgeSegmentPosition(mapPoint, snapToGrid: false);
        RenderCurrentMap(resetView: false);
        e.Handled = true;
    }

    private void UpdateActiveEdgeSegmentPosition(WpfPoint mapPoint, bool snapToGrid)
    {
        if (activeSegmentEdge is null
            || currentMap is null
            || !currentMap.Edges.Contains(activeSegmentEdge)
            || !IsEditableSegmentDraggable(activeSegmentEdge, activeSegmentIndex))
        {
            EndEdgeSegmentDrag(save: false);
            return;
        }

        activeSegmentEdge.Points = FactoryMapOrthogonalPathService.MoveSegment(
            GetEdgeStart(activeSegmentEdge),
            GetServiceEdgePoints(activeSegmentEdge),
            GetEdgeEnd(activeSegmentEdge),
            activeSegmentIndex,
            mapPoint,
            SnapGridSize,
            snapToGrid);
    }

    private void EndEdgeSegmentDrag(bool save)
    {
        var edge = activeSegmentEdge;
        var segmentIndex = activeSegmentIndex;
        ClearEdgeSegmentDragState();

        if (!save
            || currentMap is null
            || edge is null
            || !currentMap.Edges.Contains(edge)
            || !IsEditableSegmentDraggable(edge, segmentIndex))
        {
            return;
        }

        var path = GetEditableEdgePath(edge);
        var segmentStart = path[segmentIndex];
        var segmentEnd = path[segmentIndex + 1];
        var targetPoint = new WpfPoint(
            (segmentStart.X + segmentEnd.X) / 2,
            (segmentStart.Y + segmentEnd.Y) / 2);
        edge.Points = FactoryMapOrthogonalPathService.MoveSegment(
            GetEdgeStart(edge),
            GetServiceEdgePoints(edge),
            GetEdgeEnd(edge),
            segmentIndex,
            targetPoint,
            SnapGridSize,
            snapToGrid: true);
        edge.Points = FactoryMapOrthogonalPathService.Normalize(
            GetEdgeStart(edge),
            edge.Points,
            GetEdgeEnd(edge));
        selectedEdge = edge;
        selectedSegmentEdge = edge;
        selectedSegmentIndex = Math.Min(segmentIndex, Math.Max(0, GetEditableEdgePath(edge).Count - 2));
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("连线通道已调整，但地图布局保存失败。");
            return;
        }

        SetStatusText("连线通道已调整，地图布局已保存。");
    }

    private void UpdateActiveEdgePointPosition(WpfPoint mapPoint)
    {
        if (activeEdgePointEdge is null || !IsValidEdgePointIndex(activeEdgePointEdge, activeEdgePointIndex))
        {
            EndEdgePointDrag(save: false);
            return;
        }

        activeEdgePointEdge.Points = FactoryMapOrthogonalPathService.MovePoint(
            GetEdgeStart(activeEdgePointEdge),
            activeEdgePointEdge.Points,
            GetEdgeEnd(activeEdgePointEdge),
            activeEdgePointIndex,
            mapPoint,
            SnapGridSize,
            snapToGrid: false);
    }

    private void EndEdgePointDrag(bool save)
    {
        var edge = activeEdgePointEdge;
        var pointIndex = activeEdgePointIndex;
        ClearEdgePointDragState();

        if (!save || currentMap is null || edge is null || !IsValidEdgePointIndex(edge, pointIndex))
        {
            return;
        }

        var targetPoint = new WpfPoint(edge.Points[pointIndex].X, edge.Points[pointIndex].Y);
        edge.Points = FactoryMapOrthogonalPathService.MovePoint(
            GetEdgeStart(edge),
            edge.Points,
            GetEdgeEnd(edge),
            pointIndex,
            targetPoint,
            SnapGridSize,
            snapToGrid: true);
        edge.Points = FactoryMapOrthogonalPathService.Normalize(
            GetEdgeStart(edge),
            edge.Points,
            GetEdgeEnd(edge));
        selectedEdge = edge;
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("折点已移动，但地图布局保存失败。");
            return;
        }

        SetStatusText("折点已移动，地图布局已保存。");
    }

    private ContextMenu CreateEdgePointContextMenu(FactoryMapDeviceEdgeViewData edge, int pointIndex)
    {
        var deleteItem = CreateEdgeContextMenuItem("删除折点", new EdgePointHandlePayload(edge, pointIndex), isDanger: true);
        deleteItem.Click += DeleteEdgePoint_Click;

        var menu = new ContextMenu
        {
            Padding = new Thickness(0),
            Background = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(209, 213, 219)),
            BorderThickness = new Thickness(1)
        };
        ApplyModernContextMenuStyle(menu);
        menu.Items.Add(deleteItem);
        return menu;
    }

    private void DeleteEdgePoint_Click(object sender, RoutedEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        if (sender is not MenuItem { Tag: EdgePointHandlePayload payload } || currentMap is null)
        {
            return;
        }

        var edge = payload.Edge;
        var pointIndex = payload.PointIndex;
        if (!IsValidEdgePointIndex(edge, pointIndex))
        {
            return;
        }

        EndEdgePointDrag(save: false);
        edge.Points.RemoveAt(pointIndex);
        edge.Points = FactoryMapOrthogonalPathService.Normalize(
            GetEdgeStart(edge),
            edge.Points,
            GetEdgeEnd(edge));
        selectedEdge = edge;
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("折点已删除，但地图布局保存失败。");
            return;
        }

        SetStatusText("折点已删除。");
    }

    private void DeleteEdge_Click(object sender, RoutedEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        if (sender is not MenuItem { Tag: FactoryMapDeviceEdgeViewData edge } || currentMap is null)
        {
            return;
        }

        currentMap.Edges.Remove(edge);
        if (ReferenceEquals(selectedEdge, edge))
        {
            ClearEdgeSelection();
        }

        if (ReferenceEquals(activeEdgePointEdge, edge))
        {
            ClearEdgePointDragState();
        }

        if (ReferenceEquals(activeSegmentEdge, edge))
        {
            ClearEdgeSegmentDragState();
        }

        if (ReferenceEquals(selectedSegmentEdge, edge))
        {
            selectedSegmentEdge = null;
            selectedSegmentIndex = -1;
        }

        ClearPendingConnectionStart();
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("连线已删除，但地图布局保存失败。");
            return;
        }

        SetStatusText("连线已删除。");
    }

    private void Connector_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        if (sender is not FrameworkElement element || !connectorByElement.TryGetValue(element, out var connector))
        {
            return;
        }

        selectedConnector = connector;
        ClearEdgeSelection();
        ClearSelectedDeviceSelection();
        activeConnector = connector;
        activeConnectorElement = element;
        isDraggingConnector = true;
        dragStartPoint = e.GetPosition(MapViewport);
        lastDragPoint = dragStartPoint;
        MapViewport.CaptureMouse();
        MapViewport.Cursor = WpfCursors.SizeAll;
        RenderCurrentMap(resetView: false);
        e.Handled = true;
    }

    private void Connector_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!isDraggingConnector)
        {
            return;
        }

        MoveActiveConnector(e);
    }

    private void Connector_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isDraggingConnector)
        {
            return;
        }

        EndConnectorDrag();
        e.Handled = true;
    }

    private void Connector_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        if (sender is not FrameworkElement element || !connectorByElement.TryGetValue(element, out var connector))
        {
            return;
        }

        selectedConnector = connector;
        ClearEdgeSelection();
        ClearSelectedDeviceSelection();
        var menu = CreateConnectorContextMenu(connector);
        menu.PlacementTarget = element;
        menu.IsOpen = true;
        RenderCurrentMap(resetView: false);
        e.Handled = true;
    }

    private ContextMenu CreateConnectorContextMenu(FactoryMapConnectorViewNode connector)
    {
        var deleteItem = CreateEdgeContextMenuItem("删除分支点", connector, isDanger: true);
        deleteItem.Click += DeleteConnector_Click;

        var menu = new ContextMenu
        {
            Padding = new Thickness(0),
            Background = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(209, 213, 219)),
            BorderThickness = new Thickness(1)
        };
        ApplyModernContextMenuStyle(menu);
        menu.Items.Add(deleteItem);
        return menu;
    }

    private void DeleteConnector_Click(object sender, RoutedEventArgs e)
    {
        if (!isEditMode || currentMap is null)
        {
            return;
        }

        if (sender is not MenuItem { Tag: FactoryMapConnectorViewNode connector })
        {
            return;
        }

        if (!dialogService.Confirm("删除该分支点会同时删除所有连接到它的连线，是否继续？"))
        {
            return;
        }

        currentMap.Connectors.Remove(connector);
        currentMap.Edges.RemoveAll(edge => IsEndpointConnector(edge.From, connector.Id) || IsEndpointConnector(edge.To, connector.Id));
        if (ReferenceEquals(selectedConnector, connector))
        {
            selectedConnector = null;
        }

        ClearPendingConnectionStart();
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("分支点已删除，但地图布局保存失败。");
            return;
        }

        SetStatusText("分支点已删除，相关连线已同步删除。");
    }

    private void Device_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        if (sender is not Border border || !deviceByElement.TryGetValue(border, out var device))
        {
            return;
        }

        if (!FlushPendingTopologySave())
        {
            e.Handled = true;
            return;
        }

        pendingTopologyPointId = null;
        var objectRef = new FactoryMapObjectRef(FactoryMapObjectKind.Device, device.Id);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            topologySelection.Toggle(objectRef);
            RefreshSelectionVisuals();
            SetStatusText($"已选中 {topologySelection.SelectedObjects.Count} 个对象。");
            e.Handled = true;
            return;
        }

        if (topologySelection.Contains(objectRef)
            && topologySelection.SelectedObjects.Count > 1)
        {
            BeginSelectedObjectsDrag(e);
            e.Handled = true;
            return;
        }

        if (!topologySelection.Contains(objectRef))
        {
            SelectSingleDevice(device);
        }

        isDraggingDevice = true;
        interactionState.Begin(FactoryMapInteractionKind.DraggingObject);
        hasDeviceDragStarted = false;
        activeDeviceElement = border;
        activeDevice = device;
        activeDeviceStartX = device.X;
        activeDeviceStartY = device.Y;
        dragStartPoint = e.GetPosition(MapViewport);
        lastDragPoint = dragStartPoint;
        MapViewport.CaptureMouse();
        MapViewport.Cursor = WpfCursors.SizeAll;
        e.Handled = true;
    }

    private void Device_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (isEditMode)
        {
            return;
        }

        if (sender is Border { Tag: FactoryMapDeviceViewNode device })
        {
            SelectBrowseDevice(device);
            e.Handled = true;
        }
    }

    private void SelectBrowseDevice(FactoryMapDeviceViewNode device)
    {
        HighlightShortcut(device.Shortcut);
        selectShortcut(device.Shortcut);
    }

    private void BrowseModeButton_Click(object sender, RoutedEventArgs e)
    {
        SetMapMode(FactoryMapMode.Browse);
        RestoreMapFocusAfterToolbarClick();
    }

    private void EditModeButton_Click(object sender, RoutedEventArgs e)
    {
        SetMapMode(FactoryMapMode.Edit);
        RestoreMapFocusAfterToolbarClick();
    }

    private bool SetMapMode(FactoryMapMode mode)
    {
        if (IsMapBusy || isArrangingLines)
        {
            return false;
        }

        if (interactionState.Mode == mode)
        {
            UpdateMapModeVisual();
            return true;
        }

        if (interactionState.Mode == FactoryMapMode.Edit)
        {
            CompleteActivePointerInteraction();
        }

        if (interactionState.Mode == FactoryMapMode.Edit && !FlushPendingTopologySave())
        {
            return false;
        }

        ResetTransientMapInteraction(clearSelection: true);
        interactionState.SetMode(mode);
        UpdateMapModeVisual();
        RenderCurrentMap(resetView: false);
        return true;
    }

    private void ResetTransientMapInteraction(bool clearSelection)
    {
        if (MapViewport.IsMouseCaptured)
        {
            MapViewport.ReleaseMouseCapture();
        }

        isDraggingMap = false;
        isMiddleButtonPanning = false;
        isDraggingDevice = false;
        isDraggingConnector = false;
        isDraggingEdgePoint = false;
        isDraggingEdgeSegment = false;
        isDraggingSelection = false;
        isDraggingTopologyPoint = false;
        isDraggingTopologySegment = false;
        isMarqueeSelecting = false;
        isSelectionPointerDown = false;
        hasSelectionDragStarted = false;
        multiDragMapDelta = default;
        multiDragStartPositions.Clear();
        marqueeBaseSelection.Clear();
        activeTopologyPointId = null;
        activeTopologySegmentId = null;
        ClearEdgePointDragState();
        ClearEdgeSegmentDragState();
        ClearPendingConnectionStart();
        pendingTopologyPointId = null;
        RemoveSelectionRectangle();
        interactionState.Complete();
        MapViewport.Cursor = WpfCursors.Arrow;

        if (!clearSelection)
        {
            return;
        }

        topologySelection.Clear();
        ClearEdgeSelection();
        RefreshDeviceSelectionVisuals();
    }

    private void ImportMapButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsMapBusy || isArrangingLines)
        {
            return;
        }

        if (!isEditMode)
        {
            dialogService.ShowError("请先切换到编辑模式后再导入图文件。");
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON 图文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var loadResult = layoutService.LoadFromFile(dialog.FileName, getCurrentShortcuts());
        if (!loadResult.Success)
        {
            dialogService.ShowError(loadResult.ErrorMessage ?? "图文件导入失败。");
            return;
        }

        if (loadResult.AppliedDeviceCount <= 0 && loadResult.Map.Devices.Count > 0)
        {
            dialogService.ShowError("图文件中没有任何节点能匹配当前快捷项，已取消导入。");
            return;
        }

        var saveResult = layoutService.SaveToFile(getLayoutPath(), loadResult.Map);
        if (!saveResult.Success)
        {
            dialogService.ShowError(saveResult.ErrorMessage ?? "图文件导入后保存失败。");
            return;
        }

        ClearPendingConnectionStart();
        pendingTopologyPointId = null;
        RenderMap(loadResult.Map);
        mapImported?.Invoke(dialog.FileName);
        SetStatusText($"图文件已导入：应用节点 {loadResult.AppliedDeviceCount} 个，保留连线 {loadResult.KeptEdgeCount} 条，跳过节点 {loadResult.SkippedDeviceCount} 个，跳过连线 {loadResult.SkippedEdgeCount} 条。");
    }

    private void ExportMapButton_Click(object sender, RoutedEventArgs e)
    {
        if (currentMap is null)
        {
            dialogService.ShowError("当前没有可导出的地图。");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON 图文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            FileName = "factory-map.layout.json",
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var result = layoutService.SaveToFile(dialog.FileName, currentMap);
        if (!result.Success)
        {
            dialogService.ShowError(result.ErrorMessage ?? "图文件导出失败。");
            return;
        }

        SetStatusText("图文件已导出。");
    }

    private async void ArrangeLinesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanArrangeLines())
        {
            if (!isEditMode)
            {
                dialogService.ShowError("请先切换到编辑模式后再整理线路。");
            }

            return;
        }

        if (!FlushPendingTopologySave())
        {
            return;
        }

        if (!dialogService.Confirm("确定要整理当前地图的全部线路吗？\n整理会重新计算折点，但不会移动节点或独立连接点。"))
        {
            return;
        }

        var sourceMap = currentMap;
        if (sourceMap is null)
        {
            return;
        }

        var snapshot = CaptureTopologySnapshot(sourceMap);
        var candidateMap = CloneMapForLineArrangement(sourceMap);
        var operationVersion = ++arrangeLinesOperationVersion;
        var cancellationTokenSource = new CancellationTokenSource();
        arrangeLinesCancellationTokenSource = cancellationTokenSource;
        isArrangingLines = true;
        SetArrangeLinesBusyState();
        RefreshArrangeLinesButtonState();

        try
        {
            var result = await Task.Run(() =>
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                var arrangementResult = lineArrangementService.ArrangeAll(candidateMap, SnapGridSize);
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                return arrangementResult;
            }, cancellationTokenSource.Token);

            if (isWindowClosed
                || cancellationTokenSource.IsCancellationRequested
                || operationVersion != arrangeLinesOperationVersion
                || !ReferenceEquals(currentMap, sourceMap))
            {
                return;
            }

            if (!result.Success)
            {
                dialogService.ShowError(result.ErrorMessage ?? "线路整理失败。");
                return;
            }

            if (result.ArrangedRouteCount <= 0)
            {
                dialogService.ShowInfo("当前地图没有需要整理的线路。");
                return;
            }

            currentMap.ConnectionPoints = candidateMap.ConnectionPoints;
            currentMap.Segments = candidateMap.Segments;

            bool saved;
            try
            {
                saved = saveLayout(currentMap);
            }
            catch (Exception ex)
            {
                RestoreTopologySnapshot(currentMap, snapshot);
                RenderCurrentMap(resetView: false);
                dialogService.ShowError($"地图文件保存失败，已恢复整理前的线路。\n{ex.Message}");
                return;
            }

            if (!saved)
            {
                RestoreTopologySnapshot(currentMap, snapshot);
                RenderCurrentMap(resetView: false);
                dialogService.ShowError("地图文件保存失败，已恢复整理前的线路。");
                return;
            }

            topologySelection.ReplaceWith(topologySelection.SelectedObjects
                .Where(item => item.Kind == FactoryMapObjectKind.Device));
            ClearEdgeSelection();
            RenderCurrentMap(resetView: false);
            SetStatusText($"已整理 {result.ArrangedRouteCount} 条线路。");
            dialogService.ShowInfo(
                $"已整理 {result.ArrangedRouteCount} 条线路，移除 {result.RemovedBendCount} 个旧折点，创建 {result.CreatedBendCount} 个新折点。");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!isWindowClosed
                && operationVersion == arrangeLinesOperationVersion
                && ReferenceEquals(currentMap, sourceMap))
            {
                dialogService.ShowError($"线路整理失败。\n{ex.Message}");
            }
        }
        finally
        {
            cancellationTokenSource.Dispose();
            if (operationVersion == arrangeLinesOperationVersion)
            {
                arrangeLinesCancellationTokenSource = null;
                isArrangingLines = false;
                ClearArrangeLinesBusyState();
                RefreshArrangeLinesButtonState();
            }
        }
    }

    private bool CanArrangeLines()
    {
        return currentMap is not null
            && interactionState.Mode == FactoryMapMode.Edit
            && interactionState.Kind == FactoryMapInteractionKind.Idle
            && !HasTopologyConnectionDraft
            && !HasActiveMapPointerInteraction
            && !isArrangingLines
            && !IsMapBusy;
    }

    private bool HasActiveMapPointerInteraction =>
        isDraggingMap
        || isDraggingDevice
        || isDraggingConnector
        || isDraggingEdgePoint
        || isDraggingEdgeSegment
        || isDraggingSelection
        || isDraggingTopologyPoint
        || isDraggingTopologySegment
        || isMarqueeSelecting
        || isSelectionPointerDown;

    private bool IsMapBusy => DataContext is MainViewModel { IsBusy: true };

    private void RefreshArrangeLinesButtonState()
    {
        ArrangeLinesButton.IsEnabled = CanArrangeLines();
    }

    private void SetArrangeLinesBusyState()
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.BusyOverlayHost = BusyOverlayHost.Map;
        viewModel.BusyMessage = "正在整理地图线路...";
        viewModel.BusyProgressValue = 0;
        viewModel.BusyProgressMaximum = 0;
        viewModel.BusyProgressText = "正在计算端口方向和正交路径...";
        viewModel.BusyCurrentItemText = string.Empty;
        viewModel.IsBusy = true;
    }

    private void ClearArrangeLinesBusyState()
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.IsBusy = false;
        viewModel.BusyMessage = string.Empty;
        viewModel.BusyProgressValue = 0;
        viewModel.BusyProgressMaximum = 0;
        viewModel.BusyProgressText = string.Empty;
        viewModel.BusyCurrentItemText = string.Empty;
        viewModel.BusyOverlayHost = BusyOverlayHost.Main;
    }

    private void CancelArrangeLinesOperation()
    {
        if (!isArrangingLines && arrangeLinesCancellationTokenSource is null)
        {
            return;
        }

        arrangeLinesOperationVersion++;
        arrangeLinesCancellationTokenSource?.Cancel();
        arrangeLinesCancellationTokenSource = null;
        isArrangingLines = false;
        ClearArrangeLinesBusyState();
        if (!isWindowClosed)
        {
            RefreshArrangeLinesButtonState();
        }
    }

    private static FactoryMapDeviceViewData CloneMapForLineArrangement(FactoryMapDeviceViewData map)
    {
        return new FactoryMapDeviceViewData
        {
            TopologyAuthoritative = map.TopologyAuthoritative,
            Canvas = new FactoryMapCanvas
            {
                Width = map.Canvas.Width,
                Height = map.Canvas.Height
            },
            Devices = map.Devices.Select(device => new FactoryMapDeviceViewNode
            {
                Id = device.Id,
                Key = device.Key,
                Name = device.Name,
                X = device.X,
                Y = device.Y,
                Width = device.Width,
                Height = device.Height,
                Shortcut = device.Shortcut
            }).ToList(),
            ConnectionPoints = map.ConnectionPoints.Select(point => new FactoryMapConnectionPoint
            {
                Id = point.Id,
                Kind = point.Kind,
                OwnerNodeId = point.OwnerNodeId,
                Side = point.Side,
                JunctionAxis = point.JunctionAxis,
                X = point.X,
                Y = point.Y
            }).ToList(),
            Segments = map.Segments.Select(segment => new FactoryMapSegment
            {
                Id = segment.Id,
                FromPointId = segment.FromPointId,
                ToPointId = segment.ToPointId,
                ZIndex = segment.ZIndex
            }).ToList()
        };
    }

    private void DownloadAdminUiLinksButton_Click(object sender, RoutedEventArgs e)
    {
        downloadAdminUiLinks();
    }

    private void FactoryMapWindow_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (!isEditMode || currentMap is null || IsMapBusy || isArrangingLines)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (HasTopologyConnectionDraft)
            {
                pendingTopologyPointId = null;
                RenderCurrentMap(resetView: false);
                RefreshMapModeStatus();
                SetStatusText("已取消待连接状态。");
                e.Handled = true;
                return;
            }

            topologySelection.Clear();
            ClearPendingConnectionStart();
            RenderCurrentMap(resetView: false);
            SetStatusText("已清除当前选择。");
            e.Handled = true;
            return;
        }

        var selectedObjects = topologySelection.SelectedObjects.ToArray();
        if (selectedObjects.Length == 0)
        {
            return;
        }

        var shiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var requiresGridAlignment = selectedObjects.Any(objectRef =>
            objectRef.Kind == FactoryMapObjectKind.Device);
        var step = FactoryMapMovementService.GetKeyboardStep(
            shiftPressed,
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
            requiresGridAlignment);
        var snapToGrid = FactoryMapMovementService.ShouldSnapKeyboardMovement(
            shiftPressed,
            requiresGridAlignment);
        var delta = e.Key switch
        {
            Key.Left => new Vector(-step, 0),
            Key.Right => new Vector(step, 0),
            Key.Up => new Vector(0, -step),
            Key.Down => new Vector(0, step),
            _ => default
        };

        if (delta == default)
        {
            return;
        }

        var topologySnapshot = CaptureTopologySnapshot(currentMap);
        if (selectedObjects.All(IsBatchMovableObject))
        {
            var result = movementService.MoveObjects(
                currentMap,
                selectedObjects,
                delta.X,
                delta.Y,
                snapToGrid,
                SnapGridSize);
            if (!result.Success)
            {
                SetStatusText(result.ErrorMessage ?? "对象无法沿该方向移动。");
                e.Handled = true;
                return;
            }

            RenderCurrentMap(resetView: false);
            ScheduleTopologySave(topologySnapshot, "多选对象移动后保存失败。已恢复移动前状态。");
            SetStatusText($"已移动 {selectedObjects.Length} 个对象，地图布局正在保存。");
            e.Handled = true;
            return;
        }

        if (topologySelection.PrimaryObject is not { } selectedObject)
        {
            return;
        }

        var movement = movementService.MoveObject(
            currentMap,
            selectedObject,
            delta.X,
            delta.Y,
            snapToGrid,
            SnapGridSize);
        if (!movement.Success)
        {
            RestoreTopologySnapshot(currentMap, topologySnapshot);
            SetStatusText(movement.ErrorMessage ?? "对象无法沿该方向移动。");
            e.Handled = true;
            return;
        }

        RenderCurrentMap(resetView: false);
        ScheduleTopologySave(topologySnapshot, "对象移动后保存失败。已恢复移动前状态。");
        SetStatusText("对象已移动，地图布局正在保存。");
        e.Handled = true;
    }

    private bool IsBatchMovableObject(FactoryMapObjectRef objectRef)
    {
        if (objectRef.Kind == FactoryMapObjectKind.Device)
        {
            return true;
        }

        if (objectRef.Kind != FactoryMapObjectKind.ConnectionPoint || currentMap is null)
        {
            return false;
        }

        return currentMap.ConnectionPoints.Any(point =>
            string.Equals(point.Id, objectRef.Id, StringComparison.OrdinalIgnoreCase)
            && point.Kind == FactoryMapConnectionPointKinds.Free);
    }

    private void EndpointPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: EndpointPortPayload payload })
        {
            return;
        }

        HandleEndpointClickInConnectMode(payload.Endpoint, payload.Port, payload.DisplayName);
        e.Handled = true;
    }

    private void HandleEndpointClickInConnectMode(
        FactoryMapEndpointViewData clickedEndpoint,
        string clickedPort,
        string displayName)
    {
        if (currentMap is null)
        {
            return;
        }

        clickedPort = FactoryMapEndpointGeometryService.NormalizePort(clickedPort);
        if (pendingConnectionStart is null)
        {
            pendingConnectionStart = new PendingConnectionEndpoint(clickedEndpoint, clickedPort, displayName);
            RenderCurrentMap(resetView: false);
            SetStatusText($"已选择起点：{displayName} {GetPortDisplayName(clickedPort)}端口，请选择终点端口。");
            return;
        }

        if (IsSameEndpoint(pendingConnectionStart.Endpoint, clickedEndpoint)
            && string.Equals(pendingConnectionStart.Port, clickedPort, StringComparison.OrdinalIgnoreCase))
        {
            ClearPendingConnectionStart();
            SetStatusText("已取消起点，请重新选择起点。");
            return;
        }

        if (IsSameEndpoint(pendingConnectionStart.Endpoint, clickedEndpoint))
        {
            ClearPendingConnectionStart();
            SetStatusText("同一个节点不能连接到自己，已取消起点。");
            return;
        }

        if (EdgeExists(
            pendingConnectionStart.Endpoint.Kind,
            pendingConnectionStart.Endpoint.Id,
            pendingConnectionStart.Port,
            clickedEndpoint.Kind,
            clickedEndpoint.Id,
            clickedPort))
        {
            ClearPendingConnectionStart();
            SetStatusText("这条端口连线已经存在，已跳过。");
            return;
        }

        currentMap.Edges.Add(new FactoryMapDeviceEdgeViewData
        {
            From = pendingConnectionStart.Endpoint,
            FromPort = pendingConnectionStart.Port,
            To = clickedEndpoint,
            ToPort = clickedPort
        });

        ClearPendingConnectionStart();
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("连线已新增，但地图布局保存失败。");
            return;
        }

        SetStatusText("连线已新增，请继续选择起点。");
    }

    private void ClearPendingConnectionStart()
    {
        pendingConnectionStart = null;
        pendingTopologyPointId = null;
        RefreshDeviceSelectionVisuals();
    }

    private bool EdgeExists(string fromKey, string toKey)
    {
        return EdgeExists(FactoryMapEndpointKinds.Device, fromKey, FactoryMapEndpointKinds.Device, toKey);
    }

    private bool EdgeExists(string fromKind, string fromId, string toKind, string toId)
    {
        return currentMap?.Edges.Any(edge =>
            string.Equals(edge.From.Kind, fromKind, StringComparison.OrdinalIgnoreCase)
            && string.Equals(edge.From.Id?.Trim(), fromId?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(edge.To.Kind, toKind, StringComparison.OrdinalIgnoreCase)
            && string.Equals(edge.To.Id?.Trim(), toId?.Trim(), StringComparison.OrdinalIgnoreCase)) == true;
    }

    private bool EdgeExists(string fromKind, string fromId, string fromPort, string toKind, string toId, string toPort)
    {
        var normalizedFromPort = FactoryMapEndpointGeometryService.NormalizePort(fromPort);
        var normalizedToPort = FactoryMapEndpointGeometryService.NormalizePort(toPort, FactoryMapPortKinds.Left);
        return currentMap?.Edges.Any(edge =>
            string.Equals(edge.From.Kind, fromKind, StringComparison.OrdinalIgnoreCase)
            && string.Equals(edge.From.Id?.Trim(), fromId?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(edge.FromPort, normalizedFromPort, StringComparison.OrdinalIgnoreCase)
            && string.Equals(edge.To.Kind, toKind, StringComparison.OrdinalIgnoreCase)
            && string.Equals(edge.To.Id?.Trim(), toId?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(edge.ToPort, normalizedToPort, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool IsEndpointConnector(FactoryMapEndpointViewData endpoint, string connectorId)
    {
        return string.Equals(endpoint.Kind, FactoryMapEndpointKinds.Connector, StringComparison.OrdinalIgnoreCase)
            && string.Equals(endpoint.Id?.Trim(), connectorId?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateConnectorId(FactoryMapDeviceViewData map)
    {
        string id;
        do
        {
            id = "cp_" + Guid.NewGuid().ToString("N")[..10];
        }
        while (map.Connectors.Any(connector => string.Equals(connector.Id, id, StringComparison.OrdinalIgnoreCase)));

        return id;
    }

    private static bool IsSameDevice(FactoryMapDeviceViewNode first, FactoryMapDeviceViewNode second)
    {
        return string.Equals(first.Key?.Trim(), second.Key?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameEndpoint(FactoryMapEndpointViewData first, FactoryMapEndpointViewData second)
    {
        return string.Equals(first.Kind, second.Kind, StringComparison.OrdinalIgnoreCase)
            && string.Equals(first.Id?.Trim(), second.Id?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void MapViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (isDraggingMap || isDraggingDevice || currentMap is null)
        {
            return;
        }

        if (hasUserViewState)
        {
            ApplyMapTransform();
            RefreshStatusText();
            return;
        }

        RequestFitMapToView();
    }

    private void MapViewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var viewportPoint = e.GetPosition(MapViewport);
        if (ZoomMapAtViewportPoint(e.Delta, viewportPoint))
        {
            e.Handled = true;
        }
    }

    private void FactoryMapWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var fromMapViewport = IsFromMapViewport(e.OriginalSource as DependencyObject);
        if (!fromMapViewport)
        {
            return;
        }

        MapViewport_MouseWheel(sender, e);
    }

    private void FactoryMapWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (HwndSource.FromHwnd(handle) is { } source)
        {
            source.AddHook(FactoryMapWindow_WndProc);
        }

        InstallLowLevelMouseHook();
    }

    private IntPtr FactoryMapWindow_WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_MOUSEWHEEL && msg != WM_MOUSEHWHEEL)
        {
            return IntPtr.Zero;
        }

        var viewportPoint = GetMousePositionInMapViewport(lParam);
        var insideMapViewport = IsPointInsideMapViewport(viewportPoint);
        var delta = GetWheelDelta(wParam);
        if (!insideMapViewport)
        {
            return IntPtr.Zero;
        }

        if (ZoomMapAtViewportPoint(delta, viewportPoint))
        {
            handled = true;
        }

        return IntPtr.Zero;
    }

    private WpfPoint GetMousePositionInMapViewport(IntPtr lParam)
    {
        var screenPoint = GetScreenPoint(lParam);
        return MapViewport.PointFromScreen(screenPoint);
    }

    private static WpfPoint GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        return new WpfPoint(
            unchecked((short)(value & 0xffff)),
            unchecked((short)((value >> 16) & 0xffff)));
    }

    private bool IsPointInsideMapViewport(WpfPoint point)
    {
        return point.X >= 0
            && point.Y >= 0
            && point.X <= MapViewport.ActualWidth
            && point.Y <= MapViewport.ActualHeight;
    }

    private bool ZoomMapAtViewportPoint(int delta, WpfPoint viewportPoint)
    {
        if (!IsMapReady())
        {
            return false;
        }

        var oldScale = GetTotalScale();
        var targetUserScale = delta > 0
            ? userScale * ZoomFactor
            : userScale / ZoomFactor;
        targetUserScale = Clamp(targetUserScale, MinUserScale, MaxUserScale);

        if (Math.Abs(targetUserScale - userScale) < 0.0001)
        {
            return true;
        }

        var mapPointX = (viewportPoint.X - mapOffsetX) / oldScale;
        var mapPointY = (viewportPoint.Y - mapOffsetY) / oldScale;

        userScale = targetUserScale;
        hasUserViewState = true;
        var newScale = GetTotalScale();
        mapOffsetX = viewportPoint.X - mapPointX * newScale;
        mapOffsetY = viewportPoint.Y - mapPointY * newScale;
        ApplyMapTransform();
        RefreshStatusText();
        OnViewStateChanged();
        return true;
    }

    private static int GetWheelDelta(IntPtr wParam)
    {
        return unchecked((short)(((long)wParam >> 16) & 0xffff));
    }

    private void InstallLowLevelMouseHook()
    {
        if (lowLevelMouseHookHandle != IntPtr.Zero)
        {
            return;
        }

        lowLevelMouseProc = LowLevelMouseHookCallback;
        lowLevelMouseHookHandle = SetWindowsHookEx(
            WH_MOUSE_LL,
            lowLevelMouseProc,
            GetModuleHandle(null),
            0);
    }

    private void UninstallLowLevelMouseHook()
    {
        if (lowLevelMouseHookHandle == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(lowLevelMouseHookHandle);
        lowLevelMouseHookHandle = IntPtr.Zero;
        lowLevelMouseProc = null;
    }

    private IntPtr LowLevelMouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || (wParam.ToInt32() != WM_MOUSEWHEEL && wParam.ToInt32() != WM_MOUSEHWHEEL))
        {
            return CallNextHookEx(lowLevelMouseHookHandle, nCode, wParam, lParam);
        }

        var hookData = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
        var viewportPoint = MapViewport.PointFromScreen(new WpfPoint(hookData.pt.x, hookData.pt.y));
        var insideMapViewport = IsPointInsideMapViewport(viewportPoint);
        var delta = unchecked((short)(((int)hookData.mouseData >> 16) & 0xffff));

        if (!insideMapViewport || !IsActive)
        {
            return CallNextHookEx(lowLevelMouseHookHandle, nCode, wParam, lParam);
        }

        var handledByMap = false;
        Dispatcher.Invoke(() =>
        {
            handledByMap = ZoomMapAtViewportPoint(delta, viewportPoint);
        });

        return handledByMap
            ? new IntPtr(1)
            : CallNextHookEx(lowLevelMouseHookHandle, nCode, wParam, lParam);
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct POINT
    {
        public readonly int x;
        public readonly int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MSLLHOOKSTRUCT
    {
        public readonly POINT pt;
        public readonly int mouseData;
        public readonly int flags;
        public readonly int time;
        public readonly IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelMouseProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private bool IsFromMapViewport(DependencyObject? source)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, MapViewport))
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void MapViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsMapReady())
        {
            return;
        }

        if (isEditMode && Keyboard.IsKeyDown(Key.Space))
        {
            BeginMapDrag(e.GetPosition(MapViewport), middleButton: false);
            e.Handled = true;
            return;
        }

        if (IsFromDevice(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (pendingConnectionStart is not null || HasTopologyConnectionDraft)
        {
            ClearPendingConnectionStart();
            pendingTopologyPointId = null;
            RefreshSelectionVisuals();
            RefreshMapModeStatus();
        }

        if (isEditMode)
        {
            ArmSelectionRectangle(e);
            e.Handled = true;
            return;
        }

        BeginMapDrag(e.GetPosition(MapViewport), middleButton: false);
        e.Handled = true;
    }

    private void MapViewport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(RefreshArrangeLinesButtonState));
        if (e.ChangedButton != MouseButton.Middle || !IsMapReady())
        {
            return;
        }

        BeginMapDrag(e.GetPosition(MapViewport), middleButton: true);
        e.Handled = true;
    }

    private void MapViewport_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(RefreshArrangeLinesButtonState));
        if (e.ChangedButton != MouseButton.Middle || !isDraggingMap || !isMiddleButtonPanning)
        {
            return;
        }

        EndMapDrag();
        e.Handled = true;
    }

    private void BeginMapDrag(WpfPoint startPoint, bool middleButton)
    {
        isDraggingMap = true;
        isMiddleButtonPanning = middleButton;
        lastDragPoint = startPoint;
        interactionState.Begin(FactoryMapInteractionKind.Panning);
        MapViewport.CaptureMouse();
        MapViewport.Cursor = WpfCursors.SizeAll;
    }

    private void MapViewport_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode
            || isDraggingTopologyPoint
            || isDraggingTopologySegment
            || currentMap is null
            || (e.OriginalSource is not Canvas && !ReferenceEquals(e.OriginalSource, MapViewport)))
        {
            return;
        }

        var mapPoint = e.GetPosition(MapCanvas);
        var menu = CreateTopologyContextMenu();
        menu.Items.Add(CreateTopologyMenuItem("新增连接点", false, (_, _) =>
        {
            var snapshot = CaptureTopologySnapshot(currentMap);
            var result = topologyService.AddFreePoint(
                currentMap,
                mapPoint.X,
                mapPoint.Y,
                SnapGridSize);
            if (!result.Success)
            {
                SetStatusText(result.ErrorMessage ?? "新增连接点失败。");
                return;
            }

            if (!TrySaveTopologyChange(snapshot, "新增连接点后保存失败。"))
            {
                return;
            }

            topologySelection.Select(new FactoryMapObjectRef(
                FactoryMapObjectKind.ConnectionPoint,
                result.PointId ?? string.Empty));
            RenderCurrentMap(resetView: false);
            SetStatusText("普通连接点已新增并吸附到网格。");
        }));
        menu.PlacementTarget = MapViewport;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void MapViewport_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (isDraggingTopologyPoint)
        {
            UpdateTopologyPointDrag(e);
            return;
        }

        if (isDraggingTopologySegment)
        {
            e.Handled = true;
            return;
        }

        if (isDraggingConnector)
        {
            MoveActiveConnector(e);
            return;
        }

        if (isDraggingEdgePoint)
        {
            HandleEdgePointDragMove(e);
            return;
        }

        if (isDraggingEdgeSegment)
        {
            HandleEdgeSegmentDragMove(e);
            return;
        }

        if (isSelectionPointerDown)
        {
            UpdateSelectionRectangle(e);
            return;
        }

        if (isDraggingSelection)
        {
            MoveSelectedObjects(e);
            return;
        }

        if (isDraggingDevice)
        {
            MoveActiveDevice(e);
            return;
        }

        if (!isDraggingMap
            || (e.LeftButton != MouseButtonState.Pressed
                && e.MiddleButton != MouseButtonState.Pressed))
        {
            return;
        }

        var currentPoint = e.GetPosition(MapViewport);
        var delta = currentPoint - lastDragPoint;
        mapOffsetX += delta.X;
        mapOffsetY += delta.Y;
        lastDragPoint = currentPoint;
        ApplyMapTransform();
        e.Handled = true;
    }

    private void MapViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (isDraggingTopologyPoint)
        {
            EndTopologyPointDrag(e);
            e.Handled = true;
            return;
        }

        if (isDraggingTopologySegment)
        {
            EndTopologySegmentDrag(e);
            e.Handled = true;
            return;
        }

        if (isDraggingConnector)
        {
            EndConnectorDrag();
            e.Handled = true;
            return;
        }

        if (isDraggingEdgePoint)
        {
            EndEdgePointDrag(save: true);
            e.Handled = true;
            return;
        }

        if (isDraggingEdgeSegment)
        {
            EndEdgeSegmentDrag(save: true);
            e.Handled = true;
            return;
        }

        if (isSelectionPointerDown)
        {
            EndSelectionRectangle();
            e.Handled = true;
            return;
        }

        if (isDraggingSelection)
        {
            EndSelectedObjectsDrag();
            e.Handled = true;
            return;
        }

        if (isDraggingDevice)
        {
            EndDeviceDrag();
            e.Handled = true;
            return;
        }

        if (!isDraggingMap)
        {
            return;
        }

        EndMapDrag();
        e.Handled = true;
    }

    private void MapViewport_MouseLeave(object sender, WpfMouseEventArgs e)
    {
        if (!isDraggingMap
            && !isDraggingDevice
            && !isDraggingConnector
            && !isDraggingEdgePoint
            && !isDraggingEdgeSegment
            && !isDraggingTopologyPoint
            && !isDraggingTopologySegment)
        {
            MapViewport.Cursor = WpfCursors.Arrow;
        }
    }

    private void MapViewport_LostMouseCapture(object sender, WpfMouseEventArgs e)
    {
        if (isDraggingTopologyPoint)
        {
            EndTopologyPointDrag(null);
        }
        else if (isDraggingTopologySegment)
        {
            EndTopologySegmentDrag(null);
        }
        else if (isDraggingConnector)
        {
            EndConnectorDrag();
        }
        else if (isDraggingEdgePoint)
        {
            EndEdgePointDrag(save: true);
        }
        else if (isDraggingEdgeSegment)
        {
            EndEdgeSegmentDrag(save: true);
        }
        else if (isSelectionPointerDown)
        {
            EndSelectionRectangle();
        }
        else if (isDraggingSelection)
        {
            EndSelectedObjectsDrag();
        }
        else if (isDraggingDevice)
        {
            EndDeviceDrag();
        }
        else if (isDraggingMap)
        {
            EndMapDrag();
        }
    }

    private void FactoryMapWindow_Deactivated(object? sender, EventArgs e)
    {
        if (isHandlingDeactivation)
        {
            return;
        }

        isHandlingDeactivation = true;
        try
        {
            CompleteActivePointerInteraction();

            var hadConnectionDraft = HasTopologyConnectionDraft
                || pendingConnectionStart is not null;
            pendingTopologyPointId = null;
            ClearPendingConnectionStart();
            interactionState.Complete();
            if (hadConnectionDraft && currentMap is not null)
            {
                RenderCurrentMap(resetView: false);
                RefreshMapModeStatus();
            }
        }
        finally
        {
            isHandlingDeactivation = false;
        }
    }

    private void CompleteActivePointerInteraction()
    {
        if (isDraggingTopologyPoint)
        {
            EndTopologyPointDrag(null);
        }
        else if (isDraggingTopologySegment)
        {
            EndTopologySegmentDrag(null);
        }
        else if (isDraggingConnector)
        {
            EndConnectorDrag();
        }
        else if (isDraggingEdgePoint)
        {
            EndEdgePointDrag(save: true);
        }
        else if (isDraggingEdgeSegment)
        {
            EndEdgeSegmentDrag(save: true);
        }
        else if (isDraggingSelection)
        {
            EndSelectedObjectsDrag();
        }
        else if (isDraggingDevice)
        {
            EndDeviceDrag();
        }
        else if (isSelectionPointerDown)
        {
            EndSelectionRectangle();
        }
        else if (isDraggingMap)
        {
            EndMapDrag();
        }
    }

    private void UpdateTopologyPointDrag(WpfMouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || activeTopologyPointElement is null)
        {
            return;
        }

        var currentPoint = e.GetPosition(MapCanvas);
        var delta = ConstrainTopologyPointDelta(activeTopologyPointId, currentPoint - topologyDragStartMapPoint);
        var size = activeTopologyPointElement.Width;
        Canvas.SetLeft(activeTopologyPointElement, Math.Max(0, topologyPointStartX + delta.X) - size / 2);
        Canvas.SetTop(activeTopologyPointElement, Math.Max(0, topologyPointStartY + delta.Y) - size / 2);
        e.Handled = true;
    }

    private Vector ConstrainTopologyPointDelta(string? pointId, Vector delta)
    {
        var point = string.IsNullOrWhiteSpace(pointId) ? null : FindConnectionPoint(pointId);
        if (point?.Kind != FactoryMapConnectionPointKinds.Junction)
        {
            return delta;
        }

        return FactoryMapJunctionAxes.Normalize(point.JunctionAxis) switch
        {
            FactoryMapJunctionAxes.Horizontal => new Vector(delta.X, 0),
            FactoryMapJunctionAxes.Vertical => new Vector(0, delta.Y),
            _ => default
        };
    }

    private void EndTopologyPointDrag(MouseButtonEventArgs? e)
    {
        var pointId = activeTopologyPointId;
        var targetPoint = e?.GetPosition(MapCanvas) ?? topologyDragStartMapPoint;
        var delta = ConstrainTopologyPointDelta(pointId, targetPoint - topologyDragStartMapPoint);
        isDraggingTopologyPoint = false;
        activeTopologyPointId = null;
        activeTopologyPointElement = null;
        if (MapViewport.IsMouseCaptured)
        {
            MapViewport.ReleaseMouseCapture();
        }

        interactionState.Complete();
        MapViewport.Cursor = WpfCursors.Arrow;
        if (currentMap is null || string.IsNullOrWhiteSpace(pointId))
        {
            return;
        }

        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
        {
            var point = FindConnectionPoint(pointId);
            if (e is not null
                && point?.Kind is FactoryMapConnectionPointKinds.Free or FactoryMapConnectionPointKinds.Junction)
            {
                pendingTopologyPointId = pointId;
                RefreshMapModeStatus();
                SetStatusText($"已选择{GetTopologyPointDisplayName(point)}，请选择连接终点。");
            }

            RenderCurrentMap(resetView: false);
            return;
        }

        var snapshot = CaptureTopologySnapshot(currentMap);
        var result = movementService.MoveObject(
            currentMap,
            new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, pointId),
            delta.X,
            delta.Y,
            snapToGrid: true,
            SnapGridSize);
        if (!result.Success)
        {
            RenderCurrentMap(resetView: false);
            SetStatusText(result.ErrorMessage ?? "连接点移动失败。");
            return;
        }

        if (!TrySaveTopologyChange(snapshot, "连接点移动后保存失败。"))
        {
            return;
        }

        RenderCurrentMap(resetView: false);
        SetStatusText("连接点已移动并吸附到网格。");
    }

    private void EndTopologySegmentDrag(MouseButtonEventArgs? e)
    {
        var segmentId = activeTopologySegmentId;
        var targetPoint = e?.GetPosition(MapCanvas) ?? topologyDragStartMapPoint;
        var delta = targetPoint - topologyDragStartMapPoint;
        isDraggingTopologySegment = false;
        activeTopologySegmentId = null;
        if (MapViewport.IsMouseCaptured)
        {
            MapViewport.ReleaseMouseCapture();
        }

        interactionState.Complete();
        MapViewport.Cursor = WpfCursors.Arrow;
        if (currentMap is null
            || string.IsNullOrWhiteSpace(segmentId)
            || (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold))
        {
            return;
        }

        var snapshot = CaptureTopologySnapshot(currentMap);
        var result = movementService.MoveObject(
            currentMap,
            new FactoryMapObjectRef(FactoryMapObjectKind.Segment, segmentId),
            delta.X,
            delta.Y,
            snapToGrid: true,
            SnapGridSize);
        if (!result.Success)
        {
            RenderCurrentMap(resetView: false);
            SetStatusText(result.ErrorMessage ?? "线段通道移动失败。");
            return;
        }

        if (!TrySaveTopologyChange(snapshot, "线段移动后保存失败。"))
        {
            return;
        }

        RenderCurrentMap(resetView: false);
        SetStatusText("线段通道已移动并吸附到网格。");
    }

    private void ArmSelectionRectangle(MouseButtonEventArgs e)
    {
        selectionStartViewportPoint = e.GetPosition(MapViewport);
        selectionStartPoint = ViewportPointToMapPoint(selectionStartViewportPoint);
        selectionAddsToExisting = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        marqueeBaseSelection.Clear();
        if (selectionAddsToExisting)
        {
            marqueeBaseSelection.AddRange(topologySelection.SelectedObjects);
        }

        isSelectionPointerDown = true;
        isMarqueeSelecting = false;
        RemoveSelectionRectangle();
        MapViewport.CaptureMouse();
    }

    private void BeginSelectionRectangle()
    {
        selectionRectangle = new WpfRectangle
        {
            Fill = new SolidColorBrush(WpfColor.FromArgb(35, 37, 99, 235)),
            Stroke = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235)),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(selectionRectangle, selectionStartPoint.X);
        Canvas.SetTop(selectionRectangle, selectionStartPoint.Y);
        MapCanvas.Children.Add(selectionRectangle);
        isMarqueeSelecting = true;
        interactionState.Begin(FactoryMapInteractionKind.MarqueeSelecting);
        MapViewport.Cursor = WpfCursors.Cross;
    }

    private void UpdateSelectionRectangle(WpfMouseEventArgs e)
    {
        if (!isSelectionPointerDown || e.LeftButton != MouseButtonState.Pressed)
        {
            EndSelectionRectangle();
            return;
        }

        var viewportPoint = e.GetPosition(MapViewport);
        if (!isMarqueeSelecting)
        {
            if (GetDistance(viewportPoint, selectionStartViewportPoint) < DragThreshold)
            {
                return;
            }

            BeginSelectionRectangle();
        }

        if (selectionRectangle is null)
        {
            return;
        }

        var currentPoint = ViewportPointToMapPoint(viewportPoint);
        var selectionRect = CreateRect(selectionStartPoint, currentPoint);
        Canvas.SetLeft(selectionRectangle, selectionRect.Left);
        Canvas.SetTop(selectionRectangle, selectionRect.Top);
        selectionRectangle.Width = selectionRect.Width;
        selectionRectangle.Height = selectionRect.Height;
        UpdateMarqueeSelection(selectionRect);
        e.Handled = true;
    }

    private void EndSelectionRectangle()
    {
        var hadRectangle = isMarqueeSelecting;
        isSelectionPointerDown = false;
        isMarqueeSelecting = false;
        RemoveSelectionRectangle();
        marqueeBaseSelection.Clear();
        if (MapViewport.IsMouseCaptured)
        {
            MapViewport.ReleaseMouseCapture();
        }

        interactionState.Complete();
        MapViewport.Cursor = WpfCursors.Arrow;
        if (!hadRectangle && !selectionAddsToExisting)
        {
            topologySelection.Clear();
            RefreshSelectionVisuals();
        }

        SetStatusText(topologySelection.SelectedObjects.Count > 0
            ? $"已选中 {topologySelection.SelectedObjects.Count} 个对象。"
            : "未选中对象。");
    }

    private void UpdateMarqueeSelection(WpfRect selectionRect)
    {
        if (currentMap is null)
        {
            return;
        }

        var matches = marqueeSelectionService.GetSelection(
            currentMap,
            selectionRect,
            ConnectorSize);
        if (selectionAddsToExisting)
        {
            topologySelection.ReplaceWith(marqueeBaseSelection);
            topologySelection.AddRange(matches);
        }
        else
        {
            topologySelection.ReplaceWith(matches);
        }

        RefreshSelectionVisuals();
    }

    private void BeginSelectedObjectsDrag(MouseButtonEventArgs e)
    {
        var selectedObjects = GetBatchMovableSelection();
        if (selectedObjects.Count == 0)
        {
            return;
        }

        isDraggingSelection = true;
        hasSelectionDragStarted = false;
        multiDragMapDelta = default;
        dragStartPoint = e.GetPosition(MapViewport);
        lastDragPoint = dragStartPoint;
        multiDragStartPositions.Clear();
        foreach (var objectRef in selectedObjects)
        {
            if (TryGetObjectPosition(objectRef, out var position))
            {
                multiDragStartPositions[objectRef] = position;
            }
        }

        pendingTopologyPointId = null;
        interactionState.Begin(FactoryMapInteractionKind.DraggingSelection);
        MapViewport.CaptureMouse();
        MapViewport.Cursor = WpfCursors.SizeAll;
    }

    private void MoveSelectedObjects(WpfMouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndSelectedObjectsDrag();
            return;
        }

        var currentPoint = e.GetPosition(MapViewport);
        var delta = currentPoint - dragStartPoint;
        if (!hasSelectionDragStarted && delta.Length < DragThreshold)
        {
            return;
        }

        hasSelectionDragStarted = true;
        var scale = GetTotalScale();
        if (scale <= 0)
        {
            return;
        }

        multiDragMapDelta = new Vector(delta.X / scale, delta.Y / scale);
        foreach (var (objectRef, startPosition) in multiDragStartPositions)
        {
            var target = new WpfPoint(
                Math.Max(0, startPosition.X + multiDragMapDelta.X),
                Math.Max(0, startPosition.Y + multiDragMapDelta.Y));
            if (objectRef.Kind == FactoryMapObjectKind.Device)
            {
                var device = FindDevice(objectRef.Id);
                if (device is null)
                {
                    continue;
                }

                device.X = target.X;
                device.Y = target.Y;
                if (elementByDevice.TryGetValue(device, out var border))
                {
                    Canvas.SetLeft(border, device.X);
                    Canvas.SetTop(border, device.Y);
                }
                continue;
            }

            var point = FindConnectionPoint(objectRef.Id);
            if (point is null)
            {
                continue;
            }

            point.X = target.X;
            point.Y = target.Y;
            if (topologyPointElementById.TryGetValue(point.Id, out var element))
            {
                Canvas.SetLeft(element, point.X - element.Width / 2);
                Canvas.SetTop(element, point.Y - element.Height / 2);
            }
        }

        e.Handled = true;
    }

    private void MoveActiveConnector(WpfMouseEventArgs e)
    {
        if (activeConnector is null || activeConnectorElement is null || e.LeftButton != MouseButtonState.Pressed)
        {
            EndConnectorDrag();
            return;
        }

        var currentPoint = e.GetPosition(MapViewport);
        var delta = currentPoint - lastDragPoint;
        var scale = GetTotalScale();
        if (scale <= 0)
        {
            return;
        }

        activeConnector.X = Math.Max(0, activeConnector.X + delta.X / scale);
        activeConnector.Y = Math.Max(0, activeConnector.Y + delta.Y / scale);
        Canvas.SetLeft(activeConnectorElement, activeConnector.X - activeConnectorElement.Width / 2);
        Canvas.SetTop(activeConnectorElement, activeConnector.Y - activeConnectorElement.Height / 2);
        lastDragPoint = currentPoint;
        RenderCurrentMap(resetView: false);
        e.Handled = true;
    }

    private void EndConnectorDrag()
    {
        var connector = activeConnector;
        isDraggingConnector = false;
        activeConnector = null;
        activeConnectorElement = null;
        MapViewport.ReleaseMouseCapture();
        MapViewport.Cursor = WpfCursors.Arrow;

        if (currentMap is null || connector is null)
        {
            return;
        }

        connector.X = FactoryMapEditMath.ClampAndSnapToGrid(connector.X, SnapGridSize);
        connector.Y = FactoryMapEditMath.ClampAndSnapToGrid(connector.Y, SnapGridSize);
        selectedConnector = connector;
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("连接点已移动，但地图布局保存失败。");
            return;
        }

        SetStatusText("连接点已移动，地图布局已保存。");
    }

    private void EndSelectedObjectsDrag()
    {
        var selectedObjects = GetBatchMovableSelection();
        var movedCount = selectedObjects.Count;
        var startPositions = multiDragStartPositions.ToDictionary(pair => pair.Key, pair => pair.Value);
        var mapDelta = multiDragMapDelta;
        var didMove = hasSelectionDragStarted;
        isDraggingSelection = false;
        hasSelectionDragStarted = false;
        multiDragMapDelta = default;
        multiDragStartPositions.Clear();
        if (MapViewport.IsMouseCaptured)
        {
            MapViewport.ReleaseMouseCapture();
        }

        interactionState.Complete();
        MapViewport.Cursor = WpfCursors.Arrow;

        if (currentMap is null || movedCount == 0 || !didMove)
        {
            return;
        }

        foreach (var (objectRef, startPosition) in startPositions)
        {
            SetObjectPosition(objectRef, startPosition);
        }

        SynchronizeAttachedPoints(currentMap);
        var snapshot = CaptureTopologySnapshot(currentMap);
        var movement = movementService.MoveObjects(
            currentMap,
            selectedObjects,
            mapDelta.X,
            mapDelta.Y,
            snapToGrid: true,
            SnapGridSize);
        if (!movement.Success)
        {
            RestoreTopologySnapshot(currentMap, snapshot);
            RenderCurrentMap(resetView: false);
            SetStatusText(movement.ErrorMessage ?? "多选对象移动失败。");
            return;
        }

        RenderCurrentMap(resetView: false);
        if (!TrySaveTopologyChange(snapshot, "多选对象移动后保存失败。"))
        {
            return;
        }

        SetStatusText($"已移动 {movedCount} 个对象，地图布局已保存。");
    }

    private void MoveActiveDevice(WpfMouseEventArgs e)
    {
        if (activeDevice is null || activeDeviceElement is null || e.LeftButton != MouseButtonState.Pressed)
        {
            EndDeviceDrag();
            return;
        }

        var currentPoint = e.GetPosition(MapViewport);
        if (!hasDeviceDragStarted && GetDistance(currentPoint, dragStartPoint) < DragThreshold)
        {
            return;
        }

        hasDeviceDragStarted = true;
        var delta = currentPoint - lastDragPoint;
        var scale = GetTotalScale();
        if (scale <= 0)
        {
            return;
        }

        activeDevice.X += delta.X / scale;
        activeDevice.Y += delta.Y / scale;
        activeDevice.X = Math.Max(0, activeDevice.X);
        activeDevice.Y = Math.Max(0, activeDevice.Y);
        Canvas.SetLeft(activeDeviceElement, activeDevice.X);
        Canvas.SetTop(activeDeviceElement, activeDevice.Y);
        lastDragPoint = currentPoint;
        e.Handled = true;
    }

    private void EndDeviceDrag()
    {
        var shouldSave = hasDeviceDragStarted && currentMap is not null;
        var movedDevice = activeDevice;
        isDraggingDevice = false;
        hasDeviceDragStarted = false;
        activeDevice = null;
        activeDeviceElement = null;
        if (MapViewport.IsMouseCaptured)
        {
            MapViewport.ReleaseMouseCapture();
        }

        interactionState.Complete();
        MapViewport.Cursor = WpfCursors.Arrow;

        if (!shouldSave || currentMap is null)
        {
            return;
        }

        if (movedDevice is null)
        {
            return;
        }

        var targetX = movedDevice.X;
        var targetY = movedDevice.Y;
        movedDevice.X = activeDeviceStartX;
        movedDevice.Y = activeDeviceStartY;
        SynchronizeAttachedPoints(currentMap);
        var snapshot = CaptureTopologySnapshot(currentMap);
        var movement = movementService.MoveObject(
            currentMap,
            new FactoryMapObjectRef(FactoryMapObjectKind.Device, movedDevice.Id),
            targetX - activeDeviceStartX,
            targetY - activeDeviceStartY,
            snapToGrid: true,
            SnapGridSize);
        if (!movement.Success)
        {
            RestoreTopologySnapshot(currentMap, snapshot);
            RenderCurrentMap(resetView: false);
            ModeStatusText.Text = "编辑模式：节点移动失败";
            SetStatusText(movement.ErrorMessage ?? "节点移动失败。");
            return;
        }

        SelectSingleDevice(movedDevice);
        RenderCurrentMap(resetView: false);
        if (TrySaveTopologyChange(snapshot, "节点移动后保存失败。"))
        {
            ModeStatusText.Text = "编辑模式：地图布局已保存";
        }
        else
        {
            ModeStatusText.Text = "编辑模式：地图布局保存失败";
        }

        RefreshStatusText();
    }

    private void EndMapDrag()
    {
        var wasDragging = isDraggingMap;
        isDraggingMap = false;
        isMiddleButtonPanning = false;
        if (MapViewport.IsMouseCaptured)
        {
            MapViewport.ReleaseMouseCapture();
        }

        interactionState.Complete();
        MapViewport.Cursor = WpfCursors.Arrow;
        if (wasDragging)
        {
            hasUserViewState = true;
            OnViewStateChanged();
        }
    }

    private void OnViewStateChanged()
    {
        ViewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SelectSingleDevice(FactoryMapDeviceViewNode device)
    {
        if (!string.IsNullOrWhiteSpace(device.Id))
        {
            topologySelection.Select(new FactoryMapObjectRef(FactoryMapObjectKind.Device, device.Id));
        }
        RefreshDeviceSelectionVisuals();
        Focus();
    }

    private void ClearSelectedDeviceSelection()
    {
        topologySelection.ReplaceWith(topologySelection.SelectedObjects.Where(objectRef =>
            objectRef.Kind != FactoryMapObjectKind.Device));
        RefreshDeviceSelectionVisuals();
    }

    private IReadOnlyList<FactoryMapObjectRef> GetBatchMovableSelection()
    {
        return topologySelection.SelectedObjects.Where(IsBatchMovableObject).ToArray();
    }

    private FactoryMapDeviceViewNode? FindDevice(string deviceId)
    {
        return currentMap?.Devices.FirstOrDefault(device =>
            string.Equals(device.Id, deviceId, StringComparison.OrdinalIgnoreCase));
    }

    private FactoryMapConnectionPoint? FindConnectionPoint(string pointId)
    {
        return currentMap?.ConnectionPoints.FirstOrDefault(point =>
            string.Equals(point.Id, pointId, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryGetObjectPosition(FactoryMapObjectRef objectRef, out WpfPoint position)
    {
        if (objectRef.Kind == FactoryMapObjectKind.Device && FindDevice(objectRef.Id) is { } device)
        {
            position = new WpfPoint(device.X, device.Y);
            return true;
        }

        if (objectRef.Kind == FactoryMapObjectKind.ConnectionPoint
            && FindConnectionPoint(objectRef.Id) is { Kind: FactoryMapConnectionPointKinds.Free } point)
        {
            position = new WpfPoint(point.X, point.Y);
            return true;
        }

        position = default;
        return false;
    }

    private void SetObjectPosition(FactoryMapObjectRef objectRef, WpfPoint position)
    {
        if (objectRef.Kind == FactoryMapObjectKind.Device && FindDevice(objectRef.Id) is { } device)
        {
            device.X = position.X;
            device.Y = position.Y;
            return;
        }

        if (objectRef.Kind == FactoryMapObjectKind.ConnectionPoint
            && FindConnectionPoint(objectRef.Id) is { Kind: FactoryMapConnectionPointKinds.Free } point)
        {
            point.X = position.X;
            point.Y = position.Y;
        }
    }

    private void ClearEdgeSelection()
    {
        selectedEdge = null;
        selectedSegmentEdge = null;
        selectedSegmentIndex = -1;
    }

    private void ClearEdgePointDragState()
    {
        var wasDragging = isDraggingEdgePoint;
        isDraggingEdgePoint = false;
        activeEdgePointEdge = null;
        activeEdgePointIndex = -1;
        if (wasDragging && MapViewport.IsMouseCaptured)
        {
            MapViewport.ReleaseMouseCapture();
            MapViewport.Cursor = WpfCursors.Arrow;
        }
    }

    private void ClearEdgeSegmentDragState()
    {
        var wasDragging = isDraggingEdgeSegment;
        isDraggingEdgeSegment = false;
        activeSegmentEdge = null;
        activeSegmentIndex = -1;
        if (wasDragging && MapViewport.IsMouseCaptured)
        {
            MapViewport.ReleaseMouseCapture();
            MapViewport.Cursor = WpfCursors.Arrow;
        }
    }

    private void RefreshDeviceSelectionVisuals()
    {
        foreach (var (device, border) in elementByDevice)
        {
            var pendingTopologyPoint = currentMap?.ConnectionPoints.FirstOrDefault(point =>
                string.Equals(point.Id, pendingTopologyPointId, StringComparison.OrdinalIgnoreCase));
            if (pendingTopologyPoint is not null
                && pendingTopologyPoint.Kind == FactoryMapConnectionPointKinds.Attached
                && string.Equals(pendingTopologyPoint.OwnerNodeId, device.Id, StringComparison.OrdinalIgnoreCase))
            {
                ApplyDeviceConnectionStartVisual(border);
            }
            else if (pendingConnectionStart is not null
                && string.Equals(pendingConnectionStart.Endpoint.Kind, FactoryMapEndpointKinds.Device, StringComparison.OrdinalIgnoreCase)
                && string.Equals(pendingConnectionStart.Endpoint.Id, device.Key, StringComparison.OrdinalIgnoreCase))
            {
                ApplyDeviceConnectionStartVisual(border);
            }
            else if (topologySelection.Contains(
                         new FactoryMapObjectRef(FactoryMapObjectKind.Device, device.Id)))
            {
                ApplyDeviceSelectedVisual(border);
            }
            else if (IsHighlightedShortcut(device))
            {
                ApplyDeviceHighlightedVisual(border);
            }
            else
            {
                ApplyDeviceNormalVisual(border);
            }
        }
    }

    private void RefreshSelectionVisuals()
    {
        RefreshDeviceSelectionVisuals();
        foreach (var (pointId, element) in topologyPointElementById)
        {
            var isSelected = topologySelection.Contains(
                new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, pointId));
            var isPending = string.Equals(
                pendingTopologyPointId,
                pointId,
                StringComparison.OrdinalIgnoreCase);
            if (element is not Shape shape)
            {
                continue;
            }

            shape.Stroke = new SolidColorBrush(isPending
                ? WpfColor.FromRgb(22, 163, 74)
                : isSelected
                    ? WpfColor.FromRgb(37, 99, 235)
                    : WpfColor.FromRgb(59, 130, 246));
            shape.StrokeThickness = isSelected || isPending ? 2.5 : 1.5;
        }
    }

    private bool IsHighlightedShortcut(FactoryMapDeviceViewNode device)
    {
        return !string.IsNullOrWhiteSpace(highlightedShortcutKey)
            && string.Equals(device.Key?.Trim(), highlightedShortcutKey, StringComparison.OrdinalIgnoreCase);
    }

    private void RemoveSelectionRectangle()
    {
        if (selectionRectangle is not null)
        {
            MapCanvas.Children.Remove(selectionRectangle);
            selectionRectangle = null;
        }
    }

    private WpfPoint ViewportPointToMapPoint(WpfPoint point)
    {
        var scale = GetTotalScale();
        if (scale <= 0)
        {
            return new WpfPoint(0, 0);
        }

        return new WpfPoint(
            (point.X - mapOffsetX) / scale,
            (point.Y - mapOffsetY) / scale);
    }

    private static WpfRect CreateRect(WpfPoint first, WpfPoint second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        var right = Math.Max(first.X, second.X);
        var bottom = Math.Max(first.Y, second.Y);
        return new WpfRect(left, top, right - left, bottom - top);
    }

    private static WpfRect GetDeviceRect(FactoryMapDeviceViewNode device)
    {
        return new WpfRect(
            device.X,
            device.Y,
            FactoryMapNodeGeometryService.GetWidth(device),
            FactoryMapNodeGeometryService.GetHeight(device));
    }

    private static void SnapDeviceToGrid(FactoryMapDeviceViewNode device)
    {
        device.X = FactoryMapEditMath.ClampAndSnapToGrid(device.X, SnapGridSize);
        device.Y = FactoryMapEditMath.ClampAndSnapToGrid(device.Y, SnapGridSize);
    }

    internal static WpfRect ToSquareBounds(WpfRect bounds)
    {
        if (bounds.IsEmpty)
        {
            return bounds;
        }

        var side = Math.Max(bounds.Width, bounds.Height);
        var centerX = bounds.Left + bounds.Width / 2;
        var centerY = bounds.Top + bounds.Height / 2;
        return new WpfRect(centerX - side / 2, centerY - side / 2, side, side);
    }

    internal static (double Width, double Height) CalculateMapCanvasSize(
        double baseWidth,
        double baseHeight,
        double contentRight,
        double contentBottom,
        bool isEditMode)
    {
        var buffer = isEditMode ? EditCanvasBuffer : ViewPadding;
        return (Math.Max(baseWidth, contentRight + buffer), Math.Max(baseHeight, contentBottom + buffer));
    }

    private bool FitMapToView()
    {
        if (!IsMapReady() || !TryGetContentBounds(out var bounds))
        {
            return false;
        }

        var fit = FactoryMapViewportFitCalculator.Calculate(
            MapViewport.ActualWidth,
            MapViewport.ActualHeight,
            isEditMode ? bounds : ToSquareBounds(bounds),
            ViewPadding);
        userScale = 1.0;
        fitScale = fit.Scale;
        mapOffsetX = fit.OffsetX;
        mapOffsetY = fit.OffsetY;
        ApplyMapTransform();
        RefreshStatusText();
        return true;
    }

    private void RequestFitMapToView()
    {
        if (hasUserViewState)
        {
            return;
        }

        if (pendingFitToView)
        {
            return;
        }

        pendingFitToView = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            pendingFitToView = false;
            if (hasUserViewState)
            {
                return;
            }

            if (FitMapToView())
            {
                fitRetryCount = 0;
                return;
            }

            if (IsVisible && fitRetryCount < MaxFitRetryCount)
            {
                fitRetryCount++;
                RequestFitMapToView();
            }
        }), DispatcherPriority.ContextIdle);
    }

    private void ApplyMapTransform()
    {
        var scale = GetTotalScale();
        MapScaleTransform.ScaleX = scale;
        MapScaleTransform.ScaleY = scale;
        MapTranslateTransform.X = mapOffsetX;
        MapTranslateTransform.Y = mapOffsetY;
    }

    private double GetTotalScale()
    {
        return fitScale * userScale;
    }

    private bool IsMapReady()
    {
        return MapViewport.ActualWidth > 0
            && MapViewport.ActualHeight > 0
            && MapCanvas.Width > 0
            && MapCanvas.Height > 0;
    }

    private (double Width, double Height) GetEffectiveCanvasSize()
    {
        var width = currentMap?.Canvas.Width > 0 ? currentMap.Canvas.Width : 580;
        var height = currentMap?.Canvas.Height > 0 ? currentMap.Canvas.Height : 360;
        if (currentMap is null)
        {
            return (width, height);
        }

        var right = currentMap.Devices.Count == 0
            ? 0
            : currentMap.Devices.Max(device => device.X + FactoryMapNodeGeometryService.GetWidth(device));
        var bottom = currentMap.Devices.Count == 0
            ? 0
            : currentMap.Devices.Max(device => device.Y + FactoryMapNodeGeometryService.GetHeight(device));
        var independentPoints = currentMap.ConnectionPoints
            .Where(point => point.Kind != FactoryMapConnectionPointKinds.Attached)
            .ToList();
        if (independentPoints.Count > 0)
        {
            right = Math.Max(right, independentPoints.Max(point => point.X + ConnectorSize));
            bottom = Math.Max(bottom, independentPoints.Max(point => point.Y + ConnectorSize));
        }

        if (right <= 0 || bottom <= 0)
        {
            return (width, height);
        }

        return CalculateMapCanvasSize(width, height, right, bottom, isEditMode);
    }

    private bool TryGetContentBounds(out WpfRect bounds)
    {
        bounds = WpfRect.Empty;
        if (currentMap is null)
        {
            if (MapCanvas.Width <= 0 || MapCanvas.Height <= 0)
            {
                return false;
            }

            bounds = new WpfRect(0, 0, MapCanvas.Width, MapCanvas.Height);
            return true;
        }

        var leftValues = currentMap.Devices.Select(device => device.X).ToList();
        var topValues = currentMap.Devices.Select(device => device.Y).ToList();
        var rightValues = currentMap.Devices
            .Select(device => device.X + FactoryMapNodeGeometryService.GetWidth(device))
            .ToList();
        var bottomValues = currentMap.Devices
            .Select(device => device.Y + FactoryMapNodeGeometryService.GetHeight(device))
            .ToList();
        foreach (var point in currentMap.ConnectionPoints.Where(point => point.Kind != FactoryMapConnectionPointKinds.Attached))
        {
            leftValues.Add(point.X - ConnectorSize / 2);
            topValues.Add(point.Y - ConnectorSize / 2);
            rightValues.Add(point.X + ConnectorSize / 2);
            bottomValues.Add(point.Y + ConnectorSize / 2);
        }

        if (leftValues.Count == 0)
        {
            if (MapCanvas.Width <= 0 || MapCanvas.Height <= 0)
            {
                return false;
            }

            bounds = new WpfRect(0, 0, MapCanvas.Width, MapCanvas.Height);
            return true;
        }

        var left = leftValues.Min();
        var top = topValues.Min();
        var right = rightValues.Max();
        var bottom = bottomValues.Max();
        var width = right - left;
        var height = bottom - top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        bounds = new WpfRect(left, top, width, height);
        return true;
    }

    private void UpdateMapModeVisual()
    {
        ApplyToolbarModeButtonVisual(
            BrowseModeButton,
            interactionState.Mode == FactoryMapMode.Browse,
            "浏览",
            "浏览");
        ApplyToolbarModeButtonVisual(
            EditModeButton,
            interactionState.Mode == FactoryMapMode.Edit,
            "编辑",
            "编辑");
        RefreshMapModeStatus();
    }

    private void ApplyToolbarModeButtonVisual(WpfButton button, bool isActive, string inactiveText, string activeText)
    {
        button.Content = isActive ? activeText : inactiveText;
        button.Tag = isActive ? "Active" : null;
    }

    private void RefreshMapModeStatus()
    {
        RefreshArrangeLinesButtonState();
        if (HasTopologyConnectionDraft)
        {
            var statusText = interactionState.ConnectionDraft?.OriginKind == FactoryMapConnectionOriginKinds.Segment
                ? "请选择分支连接终点"
                : "请选择连接终点";
            ApplyModeStatusVisual(
                statusText,
                WpfColor.FromRgb(234, 243, 255),
                WpfColor.FromRgb(79, 143, 239),
                WpfColor.FromRgb(30, 58, 138));
            return;
        }

        if (isEditMode)
        {
            ApplyModeStatusVisual(
                "编辑模式",
                WpfColor.FromRgb(236, 253, 245),
                WpfColor.FromRgb(110, 231, 183),
                WpfColor.FromRgb(6, 95, 70));
            return;
        }

        ApplyModeStatusVisual(
            "浏览模式",
            WpfColor.FromRgb(243, 247, 252),
            WpfColor.FromRgb(215, 224, 236),
            WpfColor.FromRgb(100, 116, 139));
    }

    private void ApplyModeStatusVisual(string text, WpfColor background, WpfColor border, WpfColor foreground)
    {
        ModeStatusText.Text = text;
        ModeStatusText.Foreground = new SolidColorBrush(foreground);
        ModeStatusBadge.Background = new SolidColorBrush(background);
        ModeStatusBadge.BorderBrush = new SolidColorBrush(border);
    }

    private void RestoreMapFocusAfterToolbarClick()
    {
        var requestedGeneration = ++mapFocusRestoreGeneration;
        Dispatcher.BeginInvoke(() =>
        {
            if (requestedGeneration != mapFocusRestoreGeneration || !IsVisible || !IsActive)
            {
                return;
            }

            if (IsMouseOverMapTitleBar())
            {
                MapTitleBar.RefreshMouseHoverState("FactoryMapSkipViewportFocusMouseOverTitleBar");
                return;
            }

            if (MapViewport.Focus())
            {
                Keyboard.Focus(MapViewport);
            }
            else
            {
                Focus();
            }
        }, DispatcherPriority.Background);
    }

    private bool IsMouseOverMapTitleBar()
    {
        if (!MapTitleBar.IsVisible || MapTitleBar.ActualWidth <= 0 || MapTitleBar.ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            var point = Mouse.GetPosition(MapTitleBar);
            return point.X >= 0
                && point.X <= MapTitleBar.ActualWidth
                && point.Y >= -2
                && point.Y <= MapTitleBar.ActualHeight;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyDeviceNormalVisual(Border border)
    {
        border.Background = WpfBrushes.White;
        border.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(183, 198, 216));
        border.BorderThickness = new Thickness(1);
    }

    private static void ApplyDeviceSelectedVisual(Border border)
    {
        border.Background = new SolidColorBrush(WpfColor.FromRgb(239, 246, 255));
        border.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(59, 130, 246));
        border.BorderThickness = new Thickness(2);
    }

    private static void ApplyDeviceHighlightedVisual(Border border)
    {
        border.Background = new SolidColorBrush(WpfColor.FromRgb(239, 246, 255));
        border.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(59, 130, 246));
        border.BorderThickness = new Thickness(2);
    }

    private static void ApplyDeviceConnectionStartVisual(Border border)
    {
        border.Background = new SolidColorBrush(WpfColor.FromRgb(239, 246, 255));
        border.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235));
        border.BorderThickness = new Thickness(2);
    }

    private static WpfPoint GetDeviceRightCenter(FactoryMapDeviceViewNode device)
    {
        return new WpfPoint(
            device.X + FactoryMapNodeGeometryService.GetWidth(device),
            device.Y + FactoryMapNodeGeometryService.GetHeight(device) / 2);
    }

    private static WpfPoint GetDeviceRightCenter(FactoryMapEndpointViewData endpoint)
    {
        return endpoint.Device is not null
            ? GetDeviceRightCenter(endpoint.Device)
            : GetConnectorCenter(endpoint);
    }

    private static WpfPoint GetDeviceLeftCenter(FactoryMapDeviceViewNode device)
    {
        return new WpfPoint(
            device.X,
            device.Y + FactoryMapNodeGeometryService.GetHeight(device) / 2);
    }

    private static WpfPoint GetDeviceLeftCenter(FactoryMapEndpointViewData endpoint)
    {
        return endpoint.Device is not null
            ? GetDeviceLeftCenter(endpoint.Device)
            : GetConnectorCenter(endpoint);
    }

    private static WpfPoint GetEdgeStart(FactoryMapDeviceEdgeViewData edge)
    {
        return FactoryMapEndpointGeometryService.GetPortPoint(edge.From, edge.FromPort);
    }

    private static WpfPoint GetEdgeEnd(FactoryMapDeviceEdgeViewData edge)
    {
        return FactoryMapEndpointGeometryService.GetPortPoint(edge.To, edge.ToPort);
    }

    private static WpfPoint GetConnectorCenter(FactoryMapEndpointViewData endpoint)
    {
        return new WpfPoint(endpoint.X, endpoint.Y);
    }

    private static bool IsFromDevice(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Border { Tag: FactoryMapDeviceViewNode })
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void SetStatusText(string text)
    {
        baseStatusText = text;
        RefreshStatusText();
    }

    private void RefreshStatusText()
    {
#if DEBUG
        var bounds = TryGetContentBounds(out var contentBounds)
            ? $"{contentBounds.Width:0}x{contentBounds.Height:0}"
            : "n/a";
        StatusText.Text = FormatDebugStatusText(
            baseStatusText,
            MapViewport.ActualWidth,
            MapViewport.ActualHeight,
            GetTotalScale(),
            bounds);
#else
        StatusText.Text = baseStatusText;
#endif
    }

    private static double GetDistance(WpfPoint a, WpfPoint b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        return Math.Sqrt(x * x + y * y);
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

}
