using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;
using VSLoader.Views;
using WinForms = System.Windows.Forms;

namespace VSLoader;

public partial class MainWindow : Window
{
    private const double DefaultLayoutLeftRatio = 0.01;
    private const double DefaultLayoutTopRatio = 0.095;
    private const double DefaultLayoutMainWidthRatio = 0.50;
    private const double DefaultLayoutMainHeightRatio = 0.62;
    private const double DefaultLayoutMapHeightRatio = 0.88;
    private const double DefaultLayoutRightMarginRatio = 0.01;
    private const double DefaultLayoutGap = 0;

    private static readonly Dictionary<string, string> SortHeaderTitles = new()
    {
        ["Name"] = "名称",
        ["Description"] = "备注",
        ["SourceModuleName"] = "原始模块名",
        ["UpdatedAt"] = "更新时间"
    };

    private readonly GlobalHotkeyService _hotkeyService = new();
    private readonly FactoryMapLayoutService _factoryMapLayoutService = new();
    private readonly AppSettings _appSettings;
    private readonly AppSettingsService _appSettingsService;
    private readonly WorkspaceContext _workspaceContext;
    private readonly WindowLayoutService _windowLayoutService;
    private readonly RuntimeLayoutState _runtimeLayoutState = new();
    private readonly InitialLayoutRestoreGuard _initialLayoutRestoreGuard = new();
    private CancellationTokenSource? _layoutSaveDebounceCts;
    private WinForms.NotifyIcon? _notifyIcon;
    private FactoryMapWindow? _factoryMapWindow;
    private bool _isFactoryMapOpen;
    private bool _isExitRequested;
    private bool _hasAppliedDefaultLayout;
    private bool _isViewModelEventsAttached;
    private bool _isApplyingRuntimeLayout;
    private bool _isRestoringShortcutGridColumns;
    private bool _isShortcutGridColumnTrackingAttached;
    private bool _hasCleanedUpForClose;

    internal string WorkspaceId => _workspaceContext.Id;

    public MainWindow(AppSettings appSettings, AppSettingsService appSettingsService, WorkspaceContext workspaceContext)
    {
        _appSettings = appSettings;
        _appSettingsService = appSettingsService;
        _workspaceContext = workspaceContext;
        _windowLayoutService = new WindowLayoutService(workspaceContext.RootPath);

        InitializeComponent();
        DataContext = CreateMainViewModel();
        Title = BuildWindowTitle(workspaceContext);
        InitializeTrayIcon();
        SetInitialSortState();
        LoadWindowLayoutConfig();
        Loaded += MainWindow_Loaded;
        ContentRendered += MainWindow_ContentRendered;
        LocationChanged += MainWindow_LocationOrSizeChanged;
        SizeChanged += MainWindow_LocationOrSizeChanged;
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private MainViewModel CreateMainViewModel()
    {
        return new MainViewModel(
            _appSettings,
            _appSettingsService,
            new ConfigService(_workspaceContext.RootPath),
            new VSCodeLauncherService(),
            new DialogService(),
            new BatchImportService(),
            new AdminUiService(_workspaceContext.UiDownloadDirectory),
            new WebUiService(),
            new ShortcutSearchService(),
            new PasswordProtectionService(),
            new ClipboardService(),
            new UpdateCheckService(),
            _workspaceContext.UpdateTimePath,
            factoryMapLayoutPath: _workspaceContext.FactoryMapLayoutPath);
    }

    private static string BuildWindowTitle(WorkspaceContext workspaceContext)
    {
        var title = FormatWindowTitle(Assembly.GetExecutingAssembly().GetName().Version);
        return FormatWindowTitleWithWorkspace(title, workspaceContext.Name);
    }

    internal static string FormatWindowTitle(Version? version)
    {
        if (version is null)
        {
            return "VSLoader";
        }

        if (version.Build < 0)
        {
            return $"VSLoader v{version.Major}.{version.Minor}";
        }

        return $"VSLoader v{version.Major}.{version.Minor}.{version.Build}";
    }

    internal static string FormatWindowTitleWithWorkspace(string baseTitle, string workspaceName)
    {
        return string.IsNullOrWhiteSpace(workspaceName)
            ? baseTitle
            : $"{baseTitle} - {workspaceName}";
    }

    internal void RefreshWorkspaceTitle(string workspaceName)
    {
        var title = FormatWindowTitle(Assembly.GetExecutingAssembly().GetName().Version);
        Title = FormatWindowTitleWithWorkspace(title, workspaceName);
    }

    internal static bool ShouldRestoreFromHotkey(bool isVisible, bool isMinimized, bool isVsLoaderActive)
    {
        return !isVisible || isMinimized || !isVsLoaderActive;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (!_isViewModelEventsAttached)
        {
            viewModel.ShortcutsChanged += MainViewModel_ShortcutsChanged;
            viewModel.PropertyChanged += MainViewModel_PropertyChanged;
            _isViewModelEventsAttached = true;
        }
        _hotkeyService.Initialize(this, ToggleWindowFromHotkey);
        viewModel.TryRegisterHotkey = config =>
        {
            var result = _hotkeyService.Register(config);
            if (!result.Success)
            {
                _hotkeyService.Register(viewModel.CurrentHotkey);
            }

            return result;
        };

        var result = _hotkeyService.Register(viewModel.CurrentHotkey);
        if (!result.Success)
        {
            return;
        }

        viewModel.StartUpdateCheckLoop();
    }

    private void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ApplyDefaultWindowLayoutOnce();
            RestoreShortcutGridColumnWidths();
            AttachShortcutGridColumnWidthTracking();
        }), DispatcherPriority.ApplicationIdle);
    }

    private void LoadWindowLayoutConfig()
    {
        var config = _windowLayoutService.LoadOrCreateDefault(CreateDefaultWindowLayoutConfig, out _);
        ApplyWindowLayoutConfigToRuntimeState(config);
    }

    private WindowLayoutConfig CreateDefaultWindowLayoutConfig()
    {
        return new WindowLayoutConfig
        {
            MainWindow = ToWindowBoundsConfig(CalculateDefaultMainWindowBounds())
        };
    }

    private void ApplyDefaultWindowLayoutOnce()
    {
        if (_hasAppliedDefaultLayout)
        {
            return;
        }

        if (WindowState != WindowState.Normal)
        {
            _initialLayoutRestoreGuard.Complete();
            return;
        }

        _hasAppliedDefaultLayout = true;
        try
        {
            if (_runtimeLayoutState.HasMainWindowBounds)
            {
                RestoreMainWindowBoundsFromSession();
                return;
            }

            ApplyDefaultMainWindowLayout();
        }
        finally
        {
            _initialLayoutRestoreGuard.Complete();
        }
    }

    private void ApplyDefaultMainWindowLayout()
    {
        ApplyWindowBounds(this, CalculateDefaultMainWindowBounds());
    }

    private Rect CalculateDefaultMainWindowBounds()
    {
        var workArea = GetWorkArea();
        var maxWidth = Math.Max(MinWidth, workArea.Width * 0.70);
        var maxHeight = Math.Max(MinHeight, workArea.Height * 0.90);
        var targetWidth = Clamp(workArea.Width * DefaultLayoutMainWidthRatio, MinWidth, maxWidth);
        var targetHeight = Clamp(workArea.Height * DefaultLayoutMainHeightRatio, MinHeight, maxHeight);
        var targetLeft = workArea.Left + workArea.Width * DefaultLayoutLeftRatio;
        var targetTop = workArea.Top + workArea.Height * DefaultLayoutTopRatio;

        if (targetLeft + targetWidth > workArea.Right)
        {
            targetLeft = workArea.Right - targetWidth;
        }

        if (targetTop + targetHeight > workArea.Bottom)
        {
            targetTop = workArea.Bottom - targetHeight;
        }

        return new Rect(
            Math.Max(workArea.Left, targetLeft),
            Math.Max(workArea.Top, targetTop),
            targetWidth,
            targetHeight);
    }

    private void MainViewModel_ShortcutsChanged(object? sender, EventArgs e)
    {
        if (_factoryMapWindow is { IsVisible: true })
        {
            RefreshFactoryMap();
        }
    }

    private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedShortcut))
        {
            SyncFactoryMapSelection();
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        CleanupForClose();
    }

    private void CleanupForClose()
    {
        if (_hasCleanedUpForClose)
        {
            return;
        }

        _hasCleanedUpForClose = true;
        SaveMainWindowBoundsToSession();
        CloseFactoryMapForExit();
        SaveWindowLayoutImmediately();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.StopUpdateCheckLoop();
        }

        _hotkeyService.Dispose();
        DisposeTrayIcon();
        DetachViewModelEvents();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        e.Cancel = true;
        SaveMainWindowBoundsToSession();
        HideFactoryMapWindow();
        Hide();
    }

    private void ToggleWindowFromHotkey()
    {
        if (ShouldRestoreFromHotkey(IsVisible, WindowState == WindowState.Minimized, IsVsLoaderActive()))
        {
            RestoreAndActivate();
            return;
        }

        SaveMainWindowBoundsToSession();
        HideFactoryMapWindow();
        WindowState = WindowState.Minimized;
    }

    private bool IsVsLoaderActive()
    {
        return IsActive || _factoryMapWindow is { IsVisible: true, IsActive: true };
    }

    private void RestoreAndActivate()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            RestoreMainWindowBoundsFromSession();

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
            ShowFactoryMapIfNeeded();
        });
    }

    private void FactoryMapButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFactoryMapWindow();
    }

    private void WorkspaceButton_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.SwitchWorkspace(this);
        }
    }

    internal void PrepareForWorkspaceSwitch()
    {
        CleanupForClose();
        _isExitRequested = true;
    }

    private void ToggleFactoryMapWindow()
    {
        if (_factoryMapWindow is { IsVisible: true })
        {
            SaveFactoryMapStateToSession();
            _isFactoryMapOpen = false;
            _runtimeLayoutState.WasFactoryMapOpen = false;
            _factoryMapWindow.Close();
            _factoryMapWindow = null;
            return;
        }

        _isFactoryMapOpen = true;
        _runtimeLayoutState.WasFactoryMapOpen = true;
        ShowFactoryMapIfNeeded();
    }

    private void ShowFactoryMapIfNeeded()
    {
        if (!_isFactoryMapOpen || !IsVisible || WindowState == WindowState.Minimized)
        {
            return;
        }

        if (_factoryMapWindow is null)
        {
            _factoryMapWindow = new FactoryMapWindow(
                SelectShortcutFromMap,
                ExecuteShortcutActionFromMap,
                SaveFactoryMapLayout,
                GetCurrentShortcutsForMap,
                ResolveFactoryMapLayoutPath,
                DownloadAdminUiLinksFromMap,
                MarkMapFileUsed)
            {
                Owner = this
            };
            _factoryMapWindow.ViewStateChanged += FactoryMapWindow_ViewStateChanged;
            _factoryMapWindow.LocationChanged += FactoryMapWindow_LocationOrSizeChanged;
            _factoryMapWindow.SizeChanged += FactoryMapWindow_LocationOrSizeChanged;
            _factoryMapWindow.Closed += (_, _) =>
            {
                SaveFactoryMapStateToSession();
                _factoryMapWindow = null;
            };
        }

        PositionFactoryMapWindow(useSessionBounds: true);
        _factoryMapWindow.Show();
        RefreshFactoryMap();
    }

    private void RefreshFactoryMap()
    {
        if (_factoryMapWindow is null)
        {
            return;
        }

        if (DataContext is not MainViewModel viewModel)
        {
            _factoryMapWindow.ShowError("工厂地图无法读取当前快捷项。");
            return;
        }

        var layoutPath = ResolveFactoryMapLayoutPath();
        var loadResult = _factoryMapLayoutService.LoadOrCreate(layoutPath, viewModel.Shortcuts);
        if (!loadResult.Success)
        {
            _factoryMapWindow.ShowError(loadResult.ErrorMessage ?? "工厂地图布局读取失败。");
            return;
        }

        var hasViewState = _runtimeLayoutState.FactoryMapView is not null;
        _factoryMapWindow.RenderMap(loadResult.Map, resetView: !hasViewState);
        if (hasViewState)
        {
            _factoryMapWindow.RestoreViewState(_runtimeLayoutState.FactoryMapView);
        }

        SyncFactoryMapSelection();
    }

    private void MarkMapFileUsed(string mapFilePath)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.MarkMapFileUsed(mapFilePath);
        }
    }

    private void SyncFactoryMapSelection()
    {
        if (_factoryMapWindow is null || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        _factoryMapWindow.HighlightShortcut(viewModel.SelectedShortcut);
    }

    private void DetachViewModelEvents()
    {
        if (!_isViewModelEventsAttached || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.ShortcutsChanged -= MainViewModel_ShortcutsChanged;
        viewModel.PropertyChanged -= MainViewModel_PropertyChanged;
        _isViewModelEventsAttached = false;
    }

    private bool SaveFactoryMapLayout(VSLoader.Models.FactoryMapDeviceViewData map)
    {
        var result = _factoryMapLayoutService.Save(ResolveFactoryMapLayoutPath(), map);
        return result.Success;
    }

    private IReadOnlyList<VSLoader.Models.ShortcutItem> GetCurrentShortcutsForMap()
    {
        return DataContext is MainViewModel viewModel
            ? viewModel.Shortcuts.ToList()
            : [];
    }

    private string ResolveFactoryMapLayoutPath()
    {
        return _workspaceContext.FactoryMapLayoutPath;
    }

    private static Rect GetWorkArea()
    {
        return SystemParameters.WorkArea;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    private void PositionFactoryMapWindow(bool useSessionBounds)
    {
        if (_factoryMapWindow is null || WindowState == WindowState.Minimized)
        {
            return;
        }

        if (useSessionBounds && _runtimeLayoutState.HasFactoryMapBounds)
        {
            var sessionBounds = new Rect(
                _runtimeLayoutState.FactoryMapLeft,
                _runtimeLayoutState.FactoryMapTop,
                _runtimeLayoutState.FactoryMapWidth,
                _runtimeLayoutState.FactoryMapHeight);
            ApplyWindowBounds(_factoryMapWindow, ClampBoundsToWorkArea(
                sessionBounds,
                _factoryMapWindow.MinWidth,
                _factoryMapWindow.MinHeight));
            return;
        }

        var workArea = GetWorkArea();
        var rightMargin = workArea.Width * DefaultLayoutRightMarginRatio;
        var mainWidth = ActualWidth > 0 ? ActualWidth : Width;
        var mapLeft = Left + mainWidth + DefaultLayoutGap;
        var mapTop = Top;
        var mapHeight = Clamp(workArea.Height * DefaultLayoutMapHeightRatio, _factoryMapWindow.MinHeight, workArea.Height * 0.90);
        var availableRightWidth = workArea.Right - mapLeft - rightMargin;
        var mapWidth = Math.Max(_factoryMapWindow.MinWidth, availableRightWidth);

        if (mapLeft + mapWidth > workArea.Right)
        {
            mapWidth = Math.Max(_factoryMapWindow.MinWidth, workArea.Right - mapLeft - rightMargin);
        }

        if (mapWidth < _factoryMapWindow.MinWidth)
        {
            mapWidth = _factoryMapWindow.MinWidth;
        }

        if (mapLeft + mapWidth > workArea.Right)
        {
            mapLeft = Math.Max(workArea.Left, workArea.Right - mapWidth - rightMargin);
        }

        if (mapTop + mapHeight > workArea.Bottom)
        {
            mapTop = workArea.Bottom - mapHeight;
        }

        ApplyWindowBounds(_factoryMapWindow, new Rect(
            Math.Max(workArea.Left, mapLeft),
            Math.Max(workArea.Top, mapTop),
            mapWidth,
            mapHeight));
    }

    private void HideFactoryMapWindow()
    {
        SaveFactoryMapStateToSession();
        _factoryMapWindow?.Hide();
    }

    private void CloseFactoryMapForExit()
    {
        if (_factoryMapWindow is null)
        {
            return;
        }

        SaveFactoryMapStateToSession();
        _isFactoryMapOpen = false;
        _runtimeLayoutState.WasFactoryMapOpen = false;
        _factoryMapWindow.Close();
        _factoryMapWindow = null;
    }

    private void MainWindow_LocationOrSizeChanged(object? sender, EventArgs e)
    {
        SaveMainWindowBoundsToSession();
        if (_factoryMapWindow is { IsVisible: true } && !_runtimeLayoutState.HasFactoryMapBounds)
        {
            PositionFactoryMapWindow(useSessionBounds: false);
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            SaveMainWindowBoundsToSession();
            HideFactoryMapWindow();
            return;
        }

        ShowFactoryMapIfNeeded();
    }

    private void FactoryMapWindow_ViewStateChanged(object? sender, EventArgs e)
    {
        SaveFactoryMapStateToSession(includeViewState: true);
    }

    private void FactoryMapWindow_LocationOrSizeChanged(object? sender, EventArgs e)
    {
        if (_isApplyingRuntimeLayout)
        {
            return;
        }

        SaveFactoryMapStateToSession(includeViewState: false);
    }

    private void SaveMainWindowBoundsToSession()
    {
        if (!_initialLayoutRestoreGuard.CanSaveWindowBounds || _isApplyingRuntimeLayout || WindowState != WindowState.Normal)
        {
            return;
        }

        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _runtimeLayoutState.HasMainWindowBounds = true;
        _runtimeLayoutState.MainLeft = Left;
        _runtimeLayoutState.MainTop = Top;
        _runtimeLayoutState.MainWidth = width;
        _runtimeLayoutState.MainHeight = height;
        ScheduleWindowLayoutSave();
    }

    private void RestoreMainWindowBoundsFromSession()
    {
        if (!_runtimeLayoutState.HasMainWindowBounds)
        {
            return;
        }

        var bounds = new Rect(
            _runtimeLayoutState.MainLeft,
            _runtimeLayoutState.MainTop,
            _runtimeLayoutState.MainWidth,
            _runtimeLayoutState.MainHeight);
        ApplyWindowBounds(this, ClampBoundsToWorkArea(bounds, MinWidth, MinHeight));
    }

    private void SaveFactoryMapStateToSession(bool includeViewState = true)
    {
        if (_factoryMapWindow is null)
        {
            _runtimeLayoutState.WasFactoryMapOpen = _isFactoryMapOpen;
            return;
        }

        _runtimeLayoutState.WasFactoryMapOpen = _isFactoryMapOpen;
        var width = _factoryMapWindow.ActualWidth > 0 ? _factoryMapWindow.ActualWidth : _factoryMapWindow.Width;
        var height = _factoryMapWindow.ActualHeight > 0 ? _factoryMapWindow.ActualHeight : _factoryMapWindow.Height;
        if (width > 0 && height > 0)
        {
            _runtimeLayoutState.HasFactoryMapBounds = true;
            _runtimeLayoutState.FactoryMapLeft = _factoryMapWindow.Left;
            _runtimeLayoutState.FactoryMapTop = _factoryMapWindow.Top;
            _runtimeLayoutState.FactoryMapWidth = width;
            _runtimeLayoutState.FactoryMapHeight = height;
        }

        if (includeViewState && _factoryMapWindow.HasUserViewState)
        {
            _runtimeLayoutState.FactoryMapView = _factoryMapWindow.CaptureViewState();
        }

        ScheduleWindowLayoutSave();
    }

    private void ScheduleWindowLayoutSave()
    {
        var snapshot = BuildWindowLayoutConfigSnapshot();
        _layoutSaveDebounceCts?.Cancel();
        _layoutSaveDebounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        _layoutSaveDebounceCts = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);
                await _windowLayoutService.SaveAsync(snapshot);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // Layout persistence should never interrupt window interaction.
            }
        }, token);
    }

    private void SaveWindowLayoutImmediately()
    {
        _layoutSaveDebounceCts?.Cancel();
        _layoutSaveDebounceCts?.Dispose();
        _layoutSaveDebounceCts = null;
        _windowLayoutService.Save(BuildWindowLayoutConfigSnapshot());
    }

    private WindowLayoutConfig BuildWindowLayoutConfigSnapshot()
    {
        return new WindowLayoutConfig
        {
            MainWindow = _runtimeLayoutState.HasMainWindowBounds
                ? new WindowBoundsConfig
                {
                    Left = _runtimeLayoutState.MainLeft,
                    Top = _runtimeLayoutState.MainTop,
                    Width = _runtimeLayoutState.MainWidth,
                    Height = _runtimeLayoutState.MainHeight
                }
                : ToWindowBoundsConfig(CalculateDefaultMainWindowBounds()),
            FactoryMapWindow = _runtimeLayoutState.HasFactoryMapBounds
                ? new WindowBoundsConfig
                {
                    Left = _runtimeLayoutState.FactoryMapLeft,
                    Top = _runtimeLayoutState.FactoryMapTop,
                    Width = _runtimeLayoutState.FactoryMapWidth,
                    Height = _runtimeLayoutState.FactoryMapHeight
                }
                : null,
            WasFactoryMapOpen = _runtimeLayoutState.WasFactoryMapOpen,
            FactoryMapView = _runtimeLayoutState.FactoryMapView is null
                ? null
                : new FactoryMapViewStateConfig
                {
                    FitScale = _runtimeLayoutState.FactoryMapView.FitScale,
                    UserScale = _runtimeLayoutState.FactoryMapView.UserScale,
                    OffsetX = _runtimeLayoutState.FactoryMapView.OffsetX,
                    OffsetY = _runtimeLayoutState.FactoryMapView.OffsetY
                },
            ShortcutGridColumns = CaptureShortcutGridColumnWidthsOrFallback()
        };
    }

    private void ApplyWindowLayoutConfigToRuntimeState(WindowLayoutConfig config)
    {
        if (config.MainWindow is not null)
        {
            var bounds = ClampBoundsToWorkArea(
                ToRect(config.MainWindow),
                MinWidth,
                MinHeight);
            _runtimeLayoutState.HasMainWindowBounds = true;
            _runtimeLayoutState.MainLeft = bounds.Left;
            _runtimeLayoutState.MainTop = bounds.Top;
            _runtimeLayoutState.MainWidth = bounds.Width;
            _runtimeLayoutState.MainHeight = bounds.Height;
        }

        if (config.FactoryMapWindow is not null)
        {
            var bounds = ClampBoundsToWorkArea(
                ToRect(config.FactoryMapWindow),
                460,
                360);
            _runtimeLayoutState.HasFactoryMapBounds = true;
            _runtimeLayoutState.FactoryMapLeft = bounds.Left;
            _runtimeLayoutState.FactoryMapTop = bounds.Top;
            _runtimeLayoutState.FactoryMapWidth = bounds.Width;
            _runtimeLayoutState.FactoryMapHeight = bounds.Height;
        }

        _runtimeLayoutState.WasFactoryMapOpen = config.WasFactoryMapOpen;
        if (config.FactoryMapView is not null)
        {
            _runtimeLayoutState.FactoryMapView = new FactoryMapViewState
            {
                FitScale = config.FactoryMapView.FitScale,
                UserScale = config.FactoryMapView.UserScale,
                OffsetX = config.FactoryMapView.OffsetX,
                OffsetY = config.FactoryMapView.OffsetY
            };
        }

        _runtimeLayoutState.ShortcutGridColumns = config.ShortcutGridColumns;
    }

    private void RestoreShortcutGridColumnWidths()
    {
        var layout = _runtimeLayoutState.ShortcutGridColumns;
        if (layout is null)
        {
            return;
        }

        _isRestoringShortcutGridColumns = true;
        try
        {
            ApplyColumnWidth("Name", layout.Name);
            ApplyColumnWidth("Description", layout.Description);
            ApplyColumnWidth("SourceModuleName", layout.SourceModuleName);
            ApplyColumnWidth("UpdatedAt", layout.UpdatedAt);
        }
        finally
        {
            _isRestoringShortcutGridColumns = false;
        }
    }

    private void ApplyColumnWidth(string sortMemberPath, double? width)
    {
        if (width is null || width <= 0)
        {
            return;
        }

        var column = ShortcutsGrid.Columns.FirstOrDefault(column => column.SortMemberPath == sortMemberPath);
        if (column is null)
        {
            return;
        }

        column.Width = new DataGridLength(width.Value, DataGridLengthUnitType.Pixel);
    }

    private void AttachShortcutGridColumnWidthTracking()
    {
        if (_isShortcutGridColumnTrackingAttached)
        {
            return;
        }

        var descriptor = DependencyPropertyDescriptor.FromProperty(
            DataGridColumn.WidthProperty,
            typeof(DataGridColumn));
        if (descriptor is null)
        {
            return;
        }

        foreach (var column in ShortcutsGrid.Columns.Where(IsShortcutGridPersistedColumn))
        {
            descriptor.AddValueChanged(column, ShortcutGridColumnWidthChanged);
        }

        _isShortcutGridColumnTrackingAttached = true;
    }

    private void ShortcutGridColumnWidthChanged(object? sender, EventArgs e)
    {
        if (_isRestoringShortcutGridColumns)
        {
            return;
        }

        _runtimeLayoutState.ShortcutGridColumns = CaptureShortcutGridColumnWidths();
        ScheduleWindowLayoutSave();
    }

    private ShortcutGridColumnLayoutConfig? CaptureShortcutGridColumnWidthsOrFallback()
    {
        var captured = CaptureShortcutGridColumnWidths();
        return HasAnyColumnWidth(captured)
            ? captured
            : _runtimeLayoutState.ShortcutGridColumns;
    }

    private ShortcutGridColumnLayoutConfig CaptureShortcutGridColumnWidths()
    {
        return new ShortcutGridColumnLayoutConfig
        {
            Name = GetColumnActualWidth("Name"),
            Description = GetColumnActualWidth("Description"),
            SourceModuleName = GetColumnActualWidth("SourceModuleName"),
            UpdatedAt = GetColumnActualWidth("UpdatedAt")
        };
    }

    private double? GetColumnActualWidth(string sortMemberPath)
    {
        var column = ShortcutsGrid.Columns.FirstOrDefault(column => column.SortMemberPath == sortMemberPath);
        if (column is null || column.ActualWidth <= 0)
        {
            return null;
        }

        return Math.Round(column.ActualWidth, 2);
    }

    private static bool HasAnyColumnWidth(ShortcutGridColumnLayoutConfig config)
    {
        return config.Name > 0
            || config.Description > 0
            || config.SourceModuleName > 0
            || config.UpdatedAt > 0;
    }

    private static bool IsShortcutGridPersistedColumn(DataGridColumn column)
    {
        return column.SortMemberPath is "Name" or "Description" or "SourceModuleName" or "UpdatedAt";
    }

    private static Rect ToRect(WindowBoundsConfig bounds)
    {
        return new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    private static WindowBoundsConfig ToWindowBoundsConfig(Rect bounds)
    {
        return new WindowBoundsConfig
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height
        };
    }

    private static Rect ClampBoundsToWorkArea(Rect bounds, double minWidth, double minHeight)
    {
        var workArea = GetWorkArea();
        var width = Clamp(bounds.Width, minWidth, Math.Max(minWidth, workArea.Width));
        var height = Clamp(bounds.Height, minHeight, Math.Max(minHeight, workArea.Height));
        var left = Clamp(bounds.Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        var top = Clamp(bounds.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        return new Rect(left, top, width, height);
    }

    private void ApplyWindowBounds(Window window, Rect bounds)
    {
        _isApplyingRuntimeLayout = true;
        try
        {
            window.Left = bounds.Left;
            window.Top = bounds.Top;
            window.Width = bounds.Width;
            window.Height = bounds.Height;
        }
        finally
        {
            _isApplyingRuntimeLayout = false;
        }
    }

    private void SelectShortcutFromMap(VSLoader.Models.ShortcutItem shortcut)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(viewModel.SearchText))
        {
            viewModel.SearchText = string.Empty;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SelectShortcutInGrid(viewModel, shortcut);
            }), DispatcherPriority.Background);
            return;
        }

        SelectShortcutInGrid(viewModel, shortcut);
    }

    private void ExecuteShortcutActionFromMap(VSLoader.Models.ShortcutItem shortcut, FactoryMapShortcutAction action)
    {
        SelectShortcutFromMap(shortcut);
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        switch (action)
        {
            case FactoryMapShortcutAction.OpenVsCode:
                if (viewModel.OpenShortcutCommand.CanExecute(null))
                {
                    viewModel.OpenShortcutCommand.Execute(null);
                }
                break;
            case FactoryMapShortcutAction.OpenAdminUi:
                if (viewModel.OpenAdminUiCommand.CanExecute(null))
                {
                    viewModel.OpenAdminUiCommand.Execute(null);
                }
                break;
            case FactoryMapShortcutAction.DownloadAdminUiLink:
                if (viewModel.DownloadSelectedAdminUiLinkCommand.CanExecute(null))
                {
                    viewModel.DownloadSelectedAdminUiLinkCommand.Execute(null);
                }
                break;
            case FactoryMapShortcutAction.OpenWebUi:
                if (viewModel.OpenWebUiCommand.CanExecute(null))
                {
                    viewModel.OpenWebUiCommand.Execute(null);
                }
                break;
            case FactoryMapShortcutAction.Edit:
                if (viewModel.EditShortcutCommand.CanExecute(null))
                {
                    viewModel.EditShortcutCommand.Execute(null);
                }
                break;
            case FactoryMapShortcutAction.Delete:
                if (viewModel.DeleteShortcutCommand.CanExecute(null))
                {
                    viewModel.DeleteShortcutCommand.Execute(null);
                }
                break;
        }
    }

    private async void DownloadAdminUiLinksFromMap()
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (!FactoryMapWindow.ShouldInvokeDownloadAdminUiLinks(viewModel.DownloadAdminUiLinksCommand.CanExecute(null)))
        {
            return;
        }

        await viewModel.DownloadAdminUiLinksCommand.ExecuteAsync(null);
    }

    private void SelectShortcutInGrid(MainViewModel viewModel, VSLoader.Models.ShortcutItem shortcut)
    {
        viewModel.SelectedShortcut = shortcut;
        ShortcutsGrid.SelectedItem = shortcut;
        ShortcutsGrid.ScrollIntoView(shortcut);
        ShortcutsGrid.Focus();
    }

    private void InitializeTrayIcon()
    {
        var trayIcon = LoadTrayIcon();
        if (trayIcon is null)
        {
            return;
        }

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "VSLoader",
            Visible = true,
            ContextMenuStrip = CreateTrayMenu()
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                RestoreAndActivate();
            }
        };

        _notifyIcon.DoubleClick += (_, _) => RestoreAndActivate();
    }

    private static System.Drawing.Icon? LoadTrayIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/tomato.ico"));
        if (resource is null)
        {
            return null;
        }

        using var stream = resource.Stream;
        using var icon = new System.Drawing.Icon(stream);
        return (System.Drawing.Icon)icon.Clone();
    }

    private WinForms.ContextMenuStrip CreateTrayMenu()
    {
        var menu = new WinForms.ContextMenuStrip
        {
            BackColor = System.Drawing.Color.White,
            ForeColor = System.Drawing.Color.FromArgb(17, 24, 39),
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F),
            MinimumSize = new System.Drawing.Size(160, 0),
            Padding = new WinForms.Padding(0),
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Renderer = new TrayMenuRenderer()
        };

        var showItem = CreateTrayMenuItem("显示 VSLoader", (_, _) => RestoreAndActivate());
        var exitItem = CreateTrayMenuItem("退出", (_, _) => ExitApplication());

        menu.Items.Add(showItem);
        menu.Items.Add(exitItem);

        return menu;
    }

    private static WinForms.ToolStripMenuItem CreateTrayMenuItem(string text, EventHandler onClick)
    {
        var item = new WinForms.ToolStripMenuItem(text)
        {
            AutoSize = false,
            Width = 160,
            Height = 34,
            Padding = new WinForms.Padding(0),
            Margin = new WinForms.Padding(0)
        };

        item.Click += onClick;
        return item;
    }

    private void ExitApplication()
    {
        Dispatcher.Invoke(() =>
        {
            _isExitRequested = true;
            Close();
        });
    }

    private void DisposeTrayIcon()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private void ShortcutsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (FindVisualParent<DataGridRow>((DependencyObject)e.OriginalSource) is not null
            && viewModel.OpenShortcutCommand.CanExecute(null))
        {
            viewModel.OpenShortcutCommand.Execute(null);
        }
    }

    private void ShortcutsGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel { IsBusy: true })
        {
            e.Handled = true;
            return;
        }

        var row = FindVisualParent<DataGridRow>((DependencyObject)e.OriginalSource);
        if (row is null)
        {
            e.Handled = true;
            return;
        }

        row.Focus();
        row.IsSelected = true;
        ShortcutsGrid.SelectedItem = row.DataContext;
    }

    private void ShortcutsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || !TryGetSortField(e.Column.SortMemberPath, out var field))
        {
            e.Handled = true;
            return;
        }

        viewModel.ApplySort(field);
        UpdateSortHeaders(e.Column, viewModel.CurrentSortDirection);
        e.Handled = true;
    }

    private void SetInitialSortState()
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ApplyDefaultSort();
        }

        UpdateSortHeaders(null, null);
    }

    private void UpdateSortHeaders(DataGridColumn? sortedColumn, ListSortDirection? direction)
    {
        foreach (var column in ShortcutsGrid.Columns)
        {
            if (SortHeaderTitles.TryGetValue(column.SortMemberPath, out var title))
            {
                column.Header = title;
            }

            column.SortDirection = null;
        }

        if (sortedColumn is null || direction is null)
        {
            return;
        }

        if (SortHeaderTitles.TryGetValue(sortedColumn.SortMemberPath, out var sortedTitle))
        {
            var arrow = direction.Value == ListSortDirection.Ascending ? " ↑" : " ↓";
            sortedColumn.Header = $"{sortedTitle}{arrow}";
        }

        sortedColumn.SortDirection = direction.Value;
    }

    private static bool TryGetSortField(string sortMemberPath, out ShortcutSortField field)
    {
        field = sortMemberPath switch
        {
            "Name" => ShortcutSortField.Name,
            "Description" => ShortcutSortField.Description,
            "SourceModuleName" => ShortcutSortField.SourceModuleName,
            "UpdatedAt" => ShortcutSortField.UpdatedAt,
            _ => default
        };

        return sortMemberPath is "Name" or "Description" or "SourceModuleName" or "UpdatedAt";
    }

    private static T? FindVisualParent<T>(DependencyObject child)
        where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T typedParent)
            {
                return typedParent;
            }

            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private sealed class TrayMenuRenderer : WinForms.ToolStripProfessionalRenderer
    {
        private static readonly System.Drawing.Color BackgroundColor = System.Drawing.Color.White;
        private static readonly System.Drawing.Color BorderColor = System.Drawing.Color.FromArgb(209, 213, 219);
        private static readonly System.Drawing.Color HoverColor = System.Drawing.Color.FromArgb(243, 244, 246);
        private static readonly System.Drawing.Color TextColor = System.Drawing.Color.FromArgb(17, 24, 39);
        private const int TextLeftPadding = 14;
        private const int TextRightPadding = 14;

        protected override void OnRenderToolStripBackground(WinForms.ToolStripRenderEventArgs e)
        {
            using var brush = new System.Drawing.SolidBrush(BackgroundColor);
            e.Graphics.FillRectangle(brush, new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.ToolStrip.Size));
        }

        protected override void OnRenderToolStripBorder(WinForms.ToolStripRenderEventArgs e)
        {
            using var pen = new System.Drawing.Pen(BorderColor);
            var rectangle = new System.Drawing.Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.DrawRectangle(pen, rectangle);
        }

        protected override void OnRenderMenuItemBackground(WinForms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected)
            {
                return;
            }

            using var brush = new System.Drawing.SolidBrush(HoverColor);
            var rectangle = new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size);
            e.Graphics.FillRectangle(brush, rectangle);
        }

        protected override void OnRenderItemText(WinForms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = TextColor;
            e.TextFont = e.Item.Font;
            e.TextRectangle = new System.Drawing.Rectangle(
                TextLeftPadding,
                0,
                e.Item.Width - TextLeftPadding - TextRightPadding,
                e.Item.Height);
            e.TextFormat = WinForms.TextFormatFlags.Left
                | WinForms.TextFormatFlags.VerticalCenter
                | WinForms.TextFormatFlags.EndEllipsis
                | WinForms.TextFormatFlags.NoPrefix;

            base.OnRenderItemText(e);
        }
    }
}
