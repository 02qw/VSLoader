using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using VSLoader.Models;
using VSLoader.Services;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfCursors = System.Windows.Input.Cursors;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace VSLoader.Views;

public partial class FactoryMapWindow : Window
{
    private const double DeviceWidth = 150;
    private const double DeviceHeight = 58;
    private const double DragThreshold = 4;
    private const double SnapGridSize = 10;
    private const double ViewPadding = 28;
    private const double ZoomFactor = 1.1;
    private const double MinUserScale = 0.5;
    private const double MaxUserScale = 4.0;
    private const int MaxFitRetryCount = 12;
    private static readonly string MapDebugLogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VSLoader",
        "factory-map.debug.log");
    private readonly Action<ShortcutItem> selectShortcut;
    private readonly Action<ShortcutItem, FactoryMapShortcutAction> executeShortcutAction;
    private readonly Func<FactoryMapDeviceViewData, bool> saveLayout;
    private readonly Func<IReadOnlyList<ShortcutItem>> getCurrentShortcuts;
    private readonly Func<string> getLayoutPath;
    private readonly DialogService dialogService = new();
    private readonly FactoryMapLayoutService layoutService = new();
    private readonly Dictionary<Border, FactoryMapDeviceViewNode> deviceByElement = [];
    private readonly Dictionary<FactoryMapDeviceViewNode, Border> elementByDevice = [];
    private readonly Dictionary<FactoryMapDeviceViewNode, WpfPoint> multiDragStartPositions = [];
    private readonly HashSet<FactoryMapDeviceViewNode> selectedDevices = [];
    private bool isDraggingMap;
    private bool isDraggingDevice;
    private bool isDraggingSelectedNodes;
    private bool isEditMode;
    private bool isConnectMode;
    private bool isMultiSelectMode;
    private bool isSelectingNodes;
    private bool hasDeviceDragStarted;
    private bool hasUserViewState;
    private bool pendingFitToView;
    private int fitRetryCount;
    private Border? activeDeviceElement;
    private Border? pendingConnectionStartElement;
    private WpfRectangle? selectionRectangle;
    private FactoryMapDeviceViewData? currentMap;
    private FactoryMapDeviceViewNode? activeDevice;
    private FactoryMapDeviceViewNode? pendingConnectionStart;
    private string baseStatusText = "地图未加载";
    private string? highlightedShortcutKey;
    private double fitScale = 1.0;
    private WpfPoint dragStartPoint;
    private WpfPoint lastDragPoint;
    private WpfPoint selectionStartPoint;
    private double mapOffsetX;
    private double mapOffsetY;
    private double userScale = 1.0;

    public FactoryMapWindow(
        Action<ShortcutItem> selectShortcut,
        Action<ShortcutItem, FactoryMapShortcutAction> executeShortcutAction,
        Func<FactoryMapDeviceViewData, bool> saveLayout,
        Func<IReadOnlyList<ShortcutItem>> getCurrentShortcuts,
        Func<string> getLayoutPath)
    {
        this.selectShortcut = selectShortcut;
        this.executeShortcutAction = executeShortcutAction;
        this.saveLayout = saveLayout;
        this.getCurrentShortcuts = getCurrentShortcuts;
        this.getLayoutPath = getLayoutPath;
        InitializeComponent();
        Loaded += (_, _) => RequestFitMapToView();
        ContentRendered += (_, _) => RequestFitMapToView();
        Focusable = true;
        PreviewKeyDown += FactoryMapWindow_PreviewKeyDown;
        ResetMapDebugLog();
        WriteMapDebugLog("Constructor");
    }

    public event EventHandler? ViewStateChanged;

    public bool HasUserViewState => hasUserViewState;

    public void RenderMap(FactoryMapDeviceViewData map)
    {
        RenderMap(map, resetView: true);
    }

    public void RenderMap(FactoryMapDeviceViewData map, bool resetView)
    {
        currentMap = map;
        if (resetView)
        {
            hasUserViewState = false;
        }

        WriteMapDebugLog($"RenderMap start resetView={resetView}");
        RenderCurrentMap(resetView);
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
        WriteMapDebugLog("RestoreViewState");
    }

    public void ShowError(string message)
    {
        currentMap = new FactoryMapDeviceViewData
        {
            Canvas = new FactoryMapCanvas { Width = 580, Height = 360 }
        };
        WriteMapDebugLog("ShowError start");
        MapCanvas.Children.Clear();
        deviceByElement.Clear();
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

        MapCanvas.Children.Clear();
        selectionRectangle = null;
        deviceByElement.Clear();
        elementByDevice.Clear();
        var canvasSize = GetEffectiveCanvasSize();
        MapCanvas.Width = canvasSize.Width;
        MapCanvas.Height = canvasSize.Height;

        foreach (var edge in currentMap.Edges)
        {
            DrawEdge(edge);
        }

        foreach (var device in currentMap.Devices)
        {
            DrawDevice(device);
        }

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
        WriteMapDebugLog($"RenderCurrentMap resetView={resetView}");

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

    private void DrawEdge(FactoryMapDeviceEdgeViewData edge)
    {
        var points = CreateEdgePoints(edge);
        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(WpfColor.FromRgb(148, 163, 184)),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Points = points,
            Tag = edge
        };

        MapCanvas.Children.Add(polyline);

        var hitPolyline = new Polyline
        {
            Stroke = WpfBrushes.Transparent,
            StrokeThickness = 12,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Points = points.Clone(),
            Tag = edge
        };
        hitPolyline.PreviewMouseRightButtonDown += Edge_PreviewMouseRightButtonDown;
        MapCanvas.Children.Add(hitPolyline);
    }

    private void DrawDevice(FactoryMapDeviceViewNode device)
    {
        var deviceCode = GetDeviceCode(device);
        var displayText = string.IsNullOrWhiteSpace(deviceCode)
            ? device.Name
            : $"{device.Name}{Environment.NewLine}{deviceCode}";
        var text = new TextBlock
        {
            Text = displayText,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(15, 23, 42)),
            FontFamily = new WpfFontFamily("Microsoft YaHei UI"),
            FontSize = 12,
            LineHeight = 15,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            FontWeight = FontWeights.SemiBold
        };
        var border = new Border
        {
            Width = DeviceWidth,
            Height = DeviceHeight,
            Background = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = text,
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
        if (selectedDevices.Contains(device))
        {
            ApplyDeviceSelectedVisual(border);
        }

        MapCanvas.Children.Add(border);
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
        if (isEditMode)
        {
            return;
        }

        if (sender is not Border { Tag: FactoryMapDeviceViewNode device } border)
        {
            return;
        }

        selectShortcut(device.Shortcut);
        var menu = CreateDeviceContextMenu(device);
        menu.PlacementTarget = border;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu CreateDeviceContextMenu(FactoryMapDeviceViewNode device)
    {
        var menu = new ContextMenu
        {
            Padding = new Thickness(0),
            Background = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(209, 213, 219)),
            BorderThickness = new Thickness(1),
            Template = CreateCompactContextMenuTemplate()
        };

        menu.Items.Add(CreateDeviceMenuItem("VSCode", device, FactoryMapShortcutAction.OpenVsCode));
        menu.Items.Add(CreateDeviceMenuItem("AdminUI", device, FactoryMapShortcutAction.OpenAdminUi));
        menu.Items.Add(CreateDeviceMenuItem("获取AdminUI连接", device, FactoryMapShortcutAction.DownloadAdminUiLink));
        menu.Items.Add(CreateDeviceMenuItem("WebUI", device, FactoryMapShortcutAction.OpenWebUi));
        menu.Items.Add(CreateDeviceMenuItem("编辑", device, FactoryMapShortcutAction.Edit));
        menu.Items.Add(CreateDeviceMenuItem("删除", device, FactoryMapShortcutAction.Delete));

        return menu;
    }

    private MenuItem CreateDeviceMenuItem(string header, FactoryMapDeviceViewNode device, FactoryMapShortcutAction action)
    {
        var item = new MenuItem
        {
            Header = header,
            MinWidth = 130,
            Padding = new Thickness(14, 8, 14, 8),
            Foreground = new SolidColorBrush(WpfColor.FromRgb(17, 24, 39)),
            Background = WpfBrushes.Transparent,
            Template = CreateCompactMenuItemTemplate(),
            Tag = new DeviceContextMenuPayload(device, action)
        };

        item.Click += DeviceMenuItem_Click;
        return item;
    }

    private void DeviceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: DeviceContextMenuPayload payload })
        {
            return;
        }

        executeShortcutAction(payload.Device.Shortcut, payload.Action);
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
        var points = new PointCollection();
        var start = GetDeviceRightCenter(edge.From);
        var end = GetDeviceLeftCenter(edge.To);
        var middleX = start.X + (end.X - start.X) / 2;
        points.Add(start);
        points.Add(new WpfPoint(middleX, start.Y));
        points.Add(new WpfPoint(middleX, end.Y));
        points.Add(end);
        return points;
    }

    private ContextMenu CreateEdgeContextMenu(FactoryMapDeviceEdgeViewData edge)
    {
        var deleteItem = new MenuItem
        {
            Header = "删除连线",
            Tag = edge,
            MinWidth = 130,
            Padding = new Thickness(14, 8, 14, 8),
            Foreground = new SolidColorBrush(WpfColor.FromRgb(17, 24, 39)),
            Background = WpfBrushes.Transparent,
            Template = CreateCompactMenuItemTemplate()
        };
        deleteItem.Click += DeleteEdge_Click;

        var menu = new ContextMenu
        {
            Padding = new Thickness(0),
            Background = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(209, 213, 219)),
            BorderThickness = new Thickness(1),
            Template = CreateCompactContextMenuTemplate()
        };
        menu.Items.Add(deleteItem);
        return menu;
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

        var menu = CreateEdgeContextMenu(edge);
        menu.PlacementTarget = polyline;
        menu.IsOpen = true;
        e.Handled = true;
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
        ClearPendingConnectionStart();
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("连线已删除，但地图布局保存失败。");
            return;
        }

        SetStatusText("连线已删除。");
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

        if (isConnectMode)
        {
            HandleDeviceClickInConnectMode(border, device);
            e.Handled = true;
            return;
        }

        if (isMultiSelectMode && selectedDevices.Contains(device))
        {
            BeginSelectedDevicesDrag(e);
            e.Handled = true;
            return;
        }

        if (!isMultiSelectMode)
        {
            SelectSingleDevice(device);
        }

        isDraggingDevice = true;
        hasDeviceDragStarted = false;
        activeDeviceElement = border;
        activeDevice = device;
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
            selectShortcut(device.Shortcut);
            e.Handled = true;
        }
    }

    private void EditModeButton_Click(object sender, RoutedEventArgs e)
    {
        isEditMode = !isEditMode;
        if (!isEditMode)
        {
            isConnectMode = false;
            isMultiSelectMode = false;
            ClearPendingConnectionStart();
            ClearSelectedDevices();
            RemoveSelectionRectangle();
        }

        UpdateEditModeVisual();
        UpdateConnectModeVisual();
        UpdateMultiSelectModeVisual();
    }

    private void ConnectModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!isEditMode)
        {
            dialogService.ShowError("请先切换到编辑模式后再进行连线。");
            return;
        }

        isConnectMode = !isConnectMode;
        if (isConnectMode)
        {
            ExitMultiSelectMode(clearStatus: false);
        }

        ClearPendingConnectionStart();
        UpdateConnectModeVisual();
        ModeStatusText.Text = isConnectMode
            ? "连线模式：请选择起点设备"
            : "编辑模式：拖动设备调整位置，松开后自动保存";
    }

    private void MultiSelectModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!isEditMode)
        {
            dialogService.ShowError("请先切换到编辑模式后再进行多选。");
            return;
        }

        isMultiSelectMode = !isMultiSelectMode;
        if (isMultiSelectMode)
        {
            isConnectMode = false;
            ClearPendingConnectionStart();
            UpdateConnectModeVisual();
            ModeStatusText.Text = "多选模式：在空白区域拖动蓝色矩形框选节点";
        }
        else
        {
            ClearSelectedDevices();
            RemoveSelectionRectangle();
            ModeStatusText.Text = "编辑模式：拖动设备调整位置，松开后自动保存";
        }

        UpdateMultiSelectModeVisual();
    }

    private void ImportMapButton_Click(object sender, RoutedEventArgs e)
    {
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

        isConnectMode = false;
        ClearPendingConnectionStart();
        UpdateConnectModeVisual();
        RenderMap(loadResult.Map);
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

    private void FactoryMapWindow_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (!isEditMode || currentMap is null || selectedDevices.Count == 0)
        {
            return;
        }

        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            ? 50
            : Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                ? 1
                : 10;
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

        foreach (var device in selectedDevices)
        {
            device.X = Math.Max(0, device.X + delta.X);
            device.Y = Math.Max(0, device.Y + delta.Y);
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                SnapDeviceToGrid(device);
            }
        }

        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("节点已移动，但地图布局保存失败。");
            return;
        }

        SetStatusText($"已移动 {selectedDevices.Count} 个节点。");
        e.Handled = true;
    }

    private void HandleDeviceClickInConnectMode(Border border, FactoryMapDeviceViewNode clickedDevice)
    {
        if (currentMap is null)
        {
            return;
        }

        if (pendingConnectionStart is null)
        {
            pendingConnectionStart = clickedDevice;
            pendingConnectionStartElement = border;
            ApplyDeviceConnectionStartVisual(border);
            SetStatusText($"已选择起点：{clickedDevice.Name}，请选择终点设备。");
            return;
        }

        if (IsSameDevice(pendingConnectionStart, clickedDevice))
        {
            ClearPendingConnectionStart();
            SetStatusText("已取消起点，请重新选择起点设备。");
            return;
        }

        if (EdgeExists(pendingConnectionStart.Key, clickedDevice.Key))
        {
            ClearPendingConnectionStart();
            SetStatusText("这条连线已经存在，已跳过。");
            return;
        }

        currentMap.Edges.Add(new FactoryMapDeviceEdgeViewData
        {
            From = pendingConnectionStart,
            To = clickedDevice
        });

        ClearPendingConnectionStart();
        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("连线已新增，但地图布局保存失败。");
            return;
        }

        SetStatusText("连线已新增，请继续选择起点设备。");
    }

    private void ClearPendingConnectionStart()
    {
        if (pendingConnectionStartElement is not null)
        {
            ApplyDeviceNormalVisual(pendingConnectionStartElement);
        }

        pendingConnectionStart = null;
        pendingConnectionStartElement = null;
        RefreshDeviceSelectionVisuals();
    }

    private bool EdgeExists(string fromKey, string toKey)
    {
        return currentMap?.Edges.Any(edge =>
            string.Equals(edge.From.Key?.Trim(), fromKey?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(edge.To.Key?.Trim(), toKey?.Trim(), StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool IsSameDevice(FactoryMapDeviceViewNode first, FactoryMapDeviceViewNode second)
    {
        return string.Equals(first.Key?.Trim(), second.Key?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void MapViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        WriteMapDebugLog($"MapViewport_SizeChanged new={e.NewSize.Width:0.##}x{e.NewSize.Height:0.##}");
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
        if (!IsMapReady())
        {
            return;
        }

        var viewportPoint = e.GetPosition(MapViewport);
        var oldScale = GetTotalScale();
        var targetUserScale = e.Delta > 0
            ? userScale * ZoomFactor
            : userScale / ZoomFactor;
        targetUserScale = Clamp(targetUserScale, MinUserScale, MaxUserScale);

        if (Math.Abs(targetUserScale - userScale) < 0.0001)
        {
            e.Handled = true;
            return;
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
        WriteMapDebugLog($"MouseWheel delta={e.Delta}");
        e.Handled = true;
    }

    private void MapViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsMapReady() || IsFromDevice(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (isConnectMode && pendingConnectionStart is not null)
        {
            ClearPendingConnectionStart();
            SetStatusText("已取消起点，请重新选择起点设备。");
            e.Handled = true;
            return;
        }

        if (isMultiSelectMode)
        {
            BeginSelectionRectangle(e);
            e.Handled = true;
            return;
        }

        isDraggingMap = true;
        lastDragPoint = e.GetPosition(MapViewport);
        MapViewport.CaptureMouse();
        MapViewport.Cursor = WpfCursors.SizeAll;
        e.Handled = true;
    }

    private void MapViewport_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (isSelectingNodes)
        {
            UpdateSelectionRectangle(e);
            return;
        }

        if (isDraggingSelectedNodes)
        {
            MoveSelectedDevices(e);
            return;
        }

        if (isDraggingDevice)
        {
            MoveActiveDevice(e);
            return;
        }

        if (!isDraggingMap || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPoint = e.GetPosition(MapViewport);
        var delta = currentPoint - lastDragPoint;
        mapOffsetX += delta.X;
        mapOffsetY += delta.Y;
        lastDragPoint = currentPoint;
        ApplyMapTransform();
        WriteMapDebugLog("MapDrag move");
        e.Handled = true;
    }

    private void MapViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (isSelectingNodes)
        {
            EndSelectionRectangle();
            e.Handled = true;
            return;
        }

        if (isDraggingSelectedNodes)
        {
            EndSelectedDevicesDrag();
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
        if (!isDraggingMap && !isDraggingDevice)
        {
            MapViewport.Cursor = WpfCursors.Arrow;
        }
    }

    private void MapViewport_LostMouseCapture(object sender, WpfMouseEventArgs e)
    {
        if (isSelectingNodes)
        {
            EndSelectionRectangle();
        }
        else if (isDraggingSelectedNodes)
        {
            EndSelectedDevicesDrag();
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

    private void BeginSelectionRectangle(MouseButtonEventArgs e)
    {
        selectionStartPoint = ViewportPointToMapPoint(e.GetPosition(MapViewport));
        RemoveSelectionRectangle();
        ClearSelectedDevices();
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
        isSelectingNodes = true;
        MapViewport.CaptureMouse();
        MapViewport.Cursor = WpfCursors.Cross;
    }

    private void UpdateSelectionRectangle(WpfMouseEventArgs e)
    {
        if (selectionRectangle is null || e.LeftButton != MouseButtonState.Pressed)
        {
            EndSelectionRectangle();
            return;
        }

        var currentPoint = ViewportPointToMapPoint(e.GetPosition(MapViewport));
        var selectionRect = CreateRect(selectionStartPoint, currentPoint);
        Canvas.SetLeft(selectionRectangle, selectionRect.Left);
        Canvas.SetTop(selectionRectangle, selectionRect.Top);
        selectionRectangle.Width = selectionRect.Width;
        selectionRectangle.Height = selectionRect.Height;
        UpdateSelectedDevices(selectionRect);
        e.Handled = true;
    }

    private void EndSelectionRectangle()
    {
        isSelectingNodes = false;
        RemoveSelectionRectangle();
        MapViewport.ReleaseMouseCapture();
        MapViewport.Cursor = WpfCursors.Arrow;
        SetStatusText(selectedDevices.Count > 0
            ? $"已选中 {selectedDevices.Count} 个节点。"
            : "未选中节点。");
    }

    private void UpdateSelectedDevices(WpfRect selectionRect)
    {
        if (currentMap is null)
        {
            return;
        }

        selectedDevices.Clear();
        foreach (var device in currentMap.Devices)
        {
            var deviceRect = GetDeviceRect(device);
            if (FactoryMapEditMath.RectIntersects(selectionRect, deviceRect))
            {
                selectedDevices.Add(device);
            }
        }

        RefreshDeviceSelectionVisuals();
    }

    private void BeginSelectedDevicesDrag(MouseButtonEventArgs e)
    {
        if (selectedDevices.Count == 0)
        {
            return;
        }

        isDraggingSelectedNodes = true;
        dragStartPoint = e.GetPosition(MapViewport);
        lastDragPoint = dragStartPoint;
        multiDragStartPositions.Clear();
        foreach (var device in selectedDevices)
        {
            multiDragStartPositions[device] = new WpfPoint(device.X, device.Y);
        }

        MapViewport.CaptureMouse();
        MapViewport.Cursor = WpfCursors.SizeAll;
    }

    private void MoveSelectedDevices(WpfMouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndSelectedDevicesDrag();
            return;
        }

        var currentPoint = e.GetPosition(MapViewport);
        var delta = currentPoint - dragStartPoint;
        var scale = GetTotalScale();
        if (scale <= 0)
        {
            return;
        }

        var mapDelta = new Vector(delta.X / scale, delta.Y / scale);
        foreach (var device in selectedDevices)
        {
            if (!multiDragStartPositions.TryGetValue(device, out var startPosition))
            {
                continue;
            }

            device.X = Math.Max(0, startPosition.X + mapDelta.X);
            device.Y = Math.Max(0, startPosition.Y + mapDelta.Y);
            if (elementByDevice.TryGetValue(device, out var border))
            {
                Canvas.SetLeft(border, device.X);
                Canvas.SetTop(border, device.Y);
            }
        }

        e.Handled = true;
    }

    private void EndSelectedDevicesDrag()
    {
        var movedCount = selectedDevices.Count;
        isDraggingSelectedNodes = false;
        multiDragStartPositions.Clear();
        MapViewport.ReleaseMouseCapture();
        MapViewport.Cursor = WpfCursors.Arrow;

        if (currentMap is null || movedCount == 0)
        {
            return;
        }

        foreach (var device in selectedDevices)
        {
            SnapDeviceToGrid(device);
        }

        RenderCurrentMap(resetView: false);
        if (!saveLayout(currentMap))
        {
            dialogService.ShowError("节点已移动，但地图布局保存失败。");
            return;
        }

        SetStatusText($"已移动 {movedCount} 个节点，地图布局已保存。");
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
        MapViewport.ReleaseMouseCapture();
        MapViewport.Cursor = WpfCursors.Arrow;

        if (!shouldSave || currentMap is null)
        {
            return;
        }

        if (movedDevice is not null)
        {
            SnapDeviceToGrid(movedDevice);
            SelectSingleDevice(movedDevice);
        }

        RenderCurrentMap(resetView: false);
        if (saveLayout(currentMap))
        {
            ModeStatusText.Text = "编辑模式：地图布局已保存";
        }
        else
        {
            ModeStatusText.Text = "编辑模式：地图布局保存失败";
        }

        RefreshStatusText();
        WriteMapDebugLog("DeviceDrag saved");
    }

    private void EndMapDrag()
    {
        var wasDragging = isDraggingMap;
        isDraggingMap = false;
        MapViewport.ReleaseMouseCapture();
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
        selectedDevices.Clear();
        selectedDevices.Add(device);
        RefreshDeviceSelectionVisuals();
        Focus();
    }

    private void ClearSelectedDevices()
    {
        selectedDevices.Clear();
        RefreshDeviceSelectionVisuals();
    }

    private void RefreshDeviceSelectionVisuals()
    {
        foreach (var (device, border) in elementByDevice)
        {
            if (pendingConnectionStart is not null && IsSameDevice(pendingConnectionStart, device))
            {
                ApplyDeviceConnectionStartVisual(border);
            }
            else if (selectedDevices.Contains(device))
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

    private void ExitMultiSelectMode(bool clearStatus)
    {
        isMultiSelectMode = false;
        isSelectingNodes = false;
        isDraggingSelectedNodes = false;
        ClearSelectedDevices();
        RemoveSelectionRectangle();
        UpdateMultiSelectModeVisual();
        if (clearStatus)
        {
            ModeStatusText.Text = "编辑模式：拖动设备调整位置，松开后自动保存";
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
        return new WpfRect(device.X, device.Y, DeviceWidth, DeviceHeight);
    }

    private static void SnapDeviceToGrid(FactoryMapDeviceViewNode device)
    {
        device.X = FactoryMapEditMath.ClampAndSnapToGrid(device.X, SnapGridSize);
        device.Y = FactoryMapEditMath.ClampAndSnapToGrid(device.Y, SnapGridSize);
    }

    private bool FitMapToView()
    {
        if (!IsMapReady() || !TryGetContentBounds(out var bounds))
        {
            WriteMapDebugLog("FitMapToView not ready");
            return false;
        }

        var fit = FactoryMapViewportFitCalculator.Calculate(
            MapViewport.ActualWidth,
            MapViewport.ActualHeight,
            bounds,
            ViewPadding);
        userScale = 1.0;
        fitScale = fit.Scale;
        mapOffsetX = fit.OffsetX;
        mapOffsetY = fit.OffsetY;
        ApplyMapTransform();
        RefreshStatusText();
        WriteMapDebugLog("FitMapToView success");
        return true;
    }

    private void RequestFitMapToView()
    {
        WriteMapDebugLog("RequestFitMapToView");
        if (hasUserViewState)
        {
            WriteMapDebugLog("RequestFitMapToView skipped user view state");
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
                WriteMapDebugLog("FitMapToView callback skipped user view state");
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
        if (currentMap is null || currentMap.Devices.Count == 0)
        {
            return (width, height);
        }

        var right = currentMap.Devices.Max(device => device.X + DeviceWidth + ViewPadding);
        var bottom = currentMap.Devices.Max(device => device.Y + DeviceHeight + ViewPadding);
        return (Math.Max(width, right), Math.Max(height, bottom));
    }

    private bool TryGetContentBounds(out WpfRect bounds)
    {
        bounds = WpfRect.Empty;
        if (currentMap is null || currentMap.Devices.Count == 0)
        {
            if (MapCanvas.Width <= 0 || MapCanvas.Height <= 0)
            {
                return false;
            }

            bounds = new WpfRect(0, 0, MapCanvas.Width, MapCanvas.Height);
            return true;
        }

        var left = currentMap.Devices.Min(device => device.X);
        var top = currentMap.Devices.Min(device => device.Y);
        var right = currentMap.Devices.Max(device => device.X + DeviceWidth);
        var bottom = currentMap.Devices.Max(device => device.Y + DeviceHeight);
        var width = right - left;
        var height = bottom - top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        bounds = new WpfRect(left, top, width, height);
        return true;
    }

    private void UpdateEditModeVisual()
    {
        if (isEditMode)
        {
            EditModeButton.Content = "编辑";
            EditModeButton.Background = new SolidColorBrush(WpfColor.FromRgb(220, 252, 231));
            EditModeButton.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(134, 239, 172));
            EditModeButton.Foreground = new SolidColorBrush(WpfColor.FromRgb(22, 101, 52));
            ModeStatusText.Text = "编辑模式：拖动设备调整位置，松开后自动保存";
            return;
        }

        EditModeButton.Content = "锁定";
        EditModeButton.Background = new SolidColorBrush(WpfColor.FromRgb(254, 226, 226));
        EditModeButton.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(252, 165, 165));
        EditModeButton.Foreground = new SolidColorBrush(WpfColor.FromRgb(153, 27, 27));
        ModeStatusText.Text = "浏览模式：滚轮缩放，拖动空白区域平移地图";
    }

    private void UpdateConnectModeVisual()
    {
        if (isConnectMode)
        {
            ConnectModeButton.Background = new SolidColorBrush(WpfColor.FromRgb(219, 234, 254));
            ConnectModeButton.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(96, 165, 250));
            ConnectModeButton.Foreground = new SolidColorBrush(WpfColor.FromRgb(30, 64, 175));
            return;
        }

        ConnectModeButton.Background = WpfBrushes.White;
        ConnectModeButton.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(203, 213, 225));
        ConnectModeButton.Foreground = new SolidColorBrush(WpfColor.FromRgb(51, 65, 85));
    }

    private void UpdateMultiSelectModeVisual()
    {
        if (isMultiSelectMode)
        {
            MultiSelectModeButton.Background = new SolidColorBrush(WpfColor.FromRgb(219, 234, 254));
            MultiSelectModeButton.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(96, 165, 250));
            MultiSelectModeButton.Foreground = new SolidColorBrush(WpfColor.FromRgb(30, 64, 175));
            return;
        }

        MultiSelectModeButton.Background = WpfBrushes.White;
        MultiSelectModeButton.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(203, 213, 225));
        MultiSelectModeButton.Foreground = new SolidColorBrush(WpfColor.FromRgb(51, 65, 85));
    }

    private static void ApplyDeviceNormalVisual(Border border)
    {
        border.Background = WpfBrushes.White;
        border.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(203, 213, 225));
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
        return new WpfPoint(device.X + DeviceWidth, device.Y + DeviceHeight / 2);
    }

    private static WpfPoint GetDeviceLeftCenter(FactoryMapDeviceViewNode device)
    {
        return new WpfPoint(device.X, device.Y + DeviceHeight / 2);
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
        StatusText.Text = $"{baseStatusText} | vp:{MapViewport.ActualWidth:0}x{MapViewport.ActualHeight:0} | scale:{GetTotalScale():0.###} | bounds:{bounds}";
#else
        StatusText.Text = baseStatusText;
#endif
    }

    private void ResetMapDebugLog()
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(MapDebugLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            System.IO.File.WriteAllText(MapDebugLogPath, string.Empty);
        }
        catch
        {
            // Debug logging must never block the map window.
        }
    }

    private void WriteMapDebugLog(string stage)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(MapDebugLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            System.IO.File.AppendAllText(MapDebugLogPath, BuildMapDebugSnapshot(stage));
        }
        catch
        {
            // Debug logging must never block the map window.
        }
    }

    private string BuildMapDebugSnapshot(string stage)
    {
        var boundsText = TryGetContentBounds(out var bounds)
            ? $"ContentBounds L={bounds.Left:0.##} T={bounds.Top:0.##} R={bounds.Right:0.##} B={bounds.Bottom:0.##} W={bounds.Width:0.##} H={bounds.Height:0.##}"
            : "ContentBounds unavailable";
        var devices = currentMap?.Devices ?? [];
        var minXDevice = devices.OrderBy(device => device.X).FirstOrDefault();
        var maxXDevice = devices.OrderByDescending(device => device.X + DeviceWidth).FirstOrDefault();
        var minYDevice = devices.OrderBy(device => device.Y).FirstOrDefault();
        var maxYDevice = devices.OrderByDescending(device => device.Y + DeviceHeight).FirstOrDefault();

        return $"""
[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {stage}
Window Actual={ActualWidth:0.##}x{ActualHeight:0.##} Config={Width:0.##}x{Height:0.##} IsVisible={IsVisible}
MapViewport Actual={MapViewport.ActualWidth:0.##}x{MapViewport.ActualHeight:0.##} Render={MapViewport.RenderSize.Width:0.##}x{MapViewport.RenderSize.Height:0.##}
MapCanvas Width={MapCanvas.Width:0.##} Height={MapCanvas.Height:0.##} Actual={MapCanvas.ActualWidth:0.##}x{MapCanvas.ActualHeight:0.##}
{boundsText}
fitScale={fitScale:0.####} userScale={userScale:0.####} totalScale={GetTotalScale():0.####}
offsetX={mapOffsetX:0.##} offsetY={mapOffsetY:0.##} transformScale={MapScaleTransform.ScaleX:0.####}x{MapScaleTransform.ScaleY:0.####} transformOffset={MapTranslateTransform.X:0.##},{MapTranslateTransform.Y:0.##}
devices={devices.Count} minX={FormatDeviceSummary(minXDevice)} maxX={FormatDeviceSummary(maxXDevice)} minY={FormatDeviceSummary(minYDevice)} maxY={FormatDeviceSummary(maxYDevice)}

""";
    }

    private static string FormatDeviceSummary(FactoryMapDeviceViewNode? device)
    {
        if (device is null)
        {
            return "none";
        }

        return $"{device.Name} X={device.X:0.##} Y={device.Y:0.##}";
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

    private sealed record DeviceContextMenuPayload(FactoryMapDeviceViewNode Device, FactoryMapShortcutAction Action);
}
