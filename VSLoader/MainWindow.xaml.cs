using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;
using VSLoader.Views;
using VSLoader.Views.Controls;
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
    private const double WorkspaceMaximizedBoundsTolerance = 8;

    private static readonly Dictionary<string, string> SortHeaderTitles = new()
    {
        ["Name"] = "名称",
        ["Description"] = "备注",
        ["SourceModuleName"] = "原始模块名",
        ["UpdatedAt"] = "更新时间"
    };

    private readonly GlobalHotkeyService _hotkeyService = new();
    private readonly MainWindowInputDiagnosticLogService _inputDiagnosticLogService = new();
    private readonly FactoryMapLayoutService _factoryMapLayoutService = new();
    private readonly AppSettings _appSettings;
    private readonly AppSettingsService _appSettingsService;
    private readonly WorkspaceContext _workspaceContext;
    private readonly WindowLayoutService _windowLayoutService;
    private readonly UpdateCheckService _updateCheckService = new();
    private readonly string _updateTimePath;
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
    private bool _isClosingFactoryMapByOwner;
    private bool _isRestoringShortcutGridColumns;
    private bool _isShortcutGridColumnTrackingAttached;
    private bool _hasCleanedUpForClose;
    private bool _isShutdownInProgress;

    internal string WorkspaceId => _workspaceContext.Id;

    public MainWindow(AppSettings appSettings, AppSettingsService appSettingsService, WorkspaceContext workspaceContext)
    {
        _appSettings = appSettings;
        _appSettingsService = appSettingsService;
        _workspaceContext = workspaceContext;
        _windowLayoutService = new WindowLayoutService(workspaceContext.RootPath);
        _updateTimePath = UpdateTimePathService.GlobalUpdateTimePath;
        MigrateLegacyUpdateTimeFiles();

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
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;
        IsVisibleChanged += MainWindow_IsVisibleChanged;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewMouseDown += MainWindow_PreviewMouseDown;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private MainViewModel CreateMainViewModel()
    {
        var viewModel = new MainViewModel(
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
            _updateCheckService,
            _updateTimePath,
            factoryMapLayoutPath: _workspaceContext.FactoryMapLayoutPath,
            workspaceId: _workspaceContext.Id);
        viewModel.RequestApplicationExit = RequestRealApplicationExit;
        return viewModel;
    }

    private void MigrateLegacyUpdateTimeFiles()
    {
        var legacySources = _appSettings.Workspaces
            .Where(workspace => !string.IsNullOrWhiteSpace(workspace.Path))
            .Select(workspace => CreateLegacyUpdateTimeSource(workspace.Path))
            .Where(source => source is not null)
            .Cast<LegacyUpdateTimeSource>()
            .Append(CreateLegacyUpdateTimeSource(_workspaceContext.RootPath))
            .Where(source => source is not null)
            .Cast<LegacyUpdateTimeSource>()
            .ToArray();

        _updateCheckService.MigrateLegacyUpdateTimeFiles(_updateTimePath, legacySources);
    }

    private static LegacyUpdateTimeSource? CreateLegacyUpdateTimeSource(string workspacePath)
    {
        try
        {
            var configResult = new ConfigService(workspacePath).Load();
            return new LegacyUpdateTimeSource(
                Path.Combine(workspacePath, "updateTime.json"),
                configResult.Config.UpdateCheck.GlobalConfigPackagePath);
        }
        catch
        {
            return null;
        }
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

    internal static bool ShouldBeginShutdown(bool isShutdownInProgress)
    {
        return !isShutdownInProgress;
    }

    internal static bool ShouldCloseFactoryMapOnStateChanged(WindowState state)
    {
        return false;
    }

    internal static bool ShouldRestoreMinimizedFactoryMapOnToggle(
        bool isFactoryMapOpen,
        bool hasFactoryMapWindow,
        WindowState factoryMapWindowState)
    {
        return isFactoryMapOpen
            && hasFactoryMapWindow
            && factoryMapWindowState == WindowState.Minimized;
    }

    internal static bool ShouldRestoreFactoryMapBounds(
        bool useSessionBounds,
        bool hasFactoryMapBounds)
    {
        return useSessionBounds && hasFactoryMapBounds;
    }

    internal static FactoryMapHotkeyAction GetFactoryMapHotkeyAction(
        bool hasFactoryMapWindow,
        bool isMinimized,
        bool isActive,
        bool isBlocked)
    {
        if (isBlocked)
        {
            return FactoryMapHotkeyAction.Ignore;
        }

        if (!hasFactoryMapWindow)
        {
            return FactoryMapHotkeyAction.Open;
        }

        if (isMinimized)
        {
            return FactoryMapHotkeyAction.Restore;
        }

        return isActive ? FactoryMapHotkeyAction.Minimize : FactoryMapHotkeyAction.Activate;
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
            viewModel.WorkspaceLayoutImported += MainViewModel_WorkspaceLayoutImported;
            viewModel.PropertyChanged += MainViewModel_PropertyChanged;
            _isViewModelEventsAttached = true;
        }
        _hotkeyService.Initialize(this, ToggleWindowFromHotkey);
        viewModel.TryRegisterHotkeys = (hotkey, mapHotkey) =>
            RegisterHotkeys(hotkey, mapHotkey, viewModel.CurrentHotkey, viewModel.CurrentMapHotkey);

        var result = RegisterHotkeys(viewModel.CurrentHotkey, viewModel.CurrentMapHotkey);
        if (!result.Success)
        {
            return;
        }

        viewModel.StartUpdateCheckLoop();
    }

    private SaveResult RegisterHotkeys(
        HotkeyConfig hotkey,
        MapHotkeyConfig mapHotkey,
        HotkeyConfig? fallbackHotkey = null,
        MapHotkeyConfig? fallbackMapHotkey = null)
    {
        if (MapHotkeyService.HasSameGestureAsMainHotkey(mapHotkey, hotkey))
        {
            return SaveResult.Fail("主程序快捷键和地图快捷键不能相同。");
        }

        var hotkeyResult = _hotkeyService.Register(hotkey);
        if (!hotkeyResult.Success)
        {
            RestoreHotkeyRegistration(fallbackHotkey, fallbackMapHotkey);
            return hotkeyResult;
        }

        var mapHotkeyResult = _hotkeyService.RegisterMapHotkey(mapHotkey, ToggleFactoryMapFromGlobalHotkey);
        if (!mapHotkeyResult.Success)
        {
            RestoreHotkeyRegistration(fallbackHotkey, fallbackMapHotkey);
            return mapHotkeyResult;
        }

        return SaveResult.Ok();
    }

    private void RestoreHotkeyRegistration(HotkeyConfig? fallbackHotkey, MapHotkeyConfig? fallbackMapHotkey)
    {
        if (fallbackHotkey is not null)
        {
            _hotkeyService.Register(fallbackHotkey);
        }

        if (fallbackMapHotkey is not null)
        {
            _hotkeyService.RegisterMapHotkey(fallbackMapHotkey, ToggleFactoryMapFromGlobalHotkey);
        }
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
            }
            else
            {
                ApplyDefaultMainWindowLayout();
            }

            RestoreMainWindowPresentationState();
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

    private void MainViewModel_WorkspaceLayoutImported(object? sender, EventArgs e)
    {
        LoadWindowLayoutConfig();
        RestoreShortcutGridColumnWidths();
        if (_factoryMapWindow is not null)
        {
            if (_runtimeLayoutState.FactoryMapView is not null)
            {
                _factoryMapWindow.RestoreViewState(_runtimeLayoutState.FactoryMapView);
            }

            RestoreFactoryMapWindowState();
        }

        SaveWindowLayoutImmediately();
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
        LogWindowInputDiagnostic("Closed");
        CleanupForClose();
    }

    private void CleanupForClose()
    {
        if (_hasCleanedUpForClose)
        {
            return;
        }

        _hasCleanedUpForClose = true;
        SaveMainWindowPresentationStateToSession();
        CloseFactoryMapForExit();
        SaveWindowLayoutImmediately();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.StopUpdateCheckLoop();
            viewModel.ShutdownAdminUiAutomation();
        }

        _hotkeyService.Dispose();
        DisposeTrayIcon();
        DetachViewModelEvents();
    }

    private void RequestRealApplicationExit()
    {
        Dispatcher.InvokeAsync(ExitApplicationAsync);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        LogWindowInputDiagnostic("Closing", $"exitRequested={_isExitRequested} cancelBefore={e.Cancel}");
        if (_isExitRequested)
        {
            LogWindowInputDiagnostic("ClosingDecision", "action=Close");
            return;
        }

        e.Cancel = true;
        LogWindowInputDiagnostic("ClosingDecision", "action=Hide cancel=True");
        SaveMainWindowPresentationStateToSession();
        SaveWindowLayoutImmediately();
        Hide();
        LogWindowInputDiagnostic("Hidden", "source=MainWindowClosing");
    }

    private void ToggleWindowFromHotkey()
    {
        LogWindowInputDiagnostic("MainHotkeyCallback", "configured=Alt+V");
        if (ShouldRestoreFromHotkey(IsVisible, WindowState == WindowState.Minimized, IsVsLoaderActive()))
        {
            RestoreAndActivate();
            return;
        }

        SaveMainWindowPresentationStateToSession();
        WindowState = WindowState.Minimized;
    }

    private bool IsVsLoaderActive()
    {
        return IsActive;
    }

    private void RestoreAndActivate()
    {
        Dispatcher.Invoke(() =>
        {
            RestoreMainWindowForHotkey();

            ActivateMainWindow();
        });
    }

    private void RestoreMainWindowForHotkey()
    {
        Show();

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        RestoreMainWindowBoundsFromSession();
        RestoreMainWindowPresentationState();
    }

    private void ActivateMainWindow()
    {
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void ActivateFactoryMapWindow()
    {
        if (_factoryMapWindow is null)
        {
            ActivateMainWindow();
            return;
        }

        _factoryMapWindow.Activate();
        _factoryMapWindow.Topmost = true;
        _factoryMapWindow.Topmost = false;
        _factoryMapWindow.Focus();
        _factoryMapWindow.Dispatcher.BeginInvoke(
            () => _factoryMapWindow.RestoreMapInputFocus(),
            DispatcherPriority.ContextIdle);
    }

    private void FactoryMapButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFactoryMapWindow();
    }

    private void ToolbarButton_Click(object sender, RoutedEventArgs e)
    {
        RestoreMainContentFocusAfterToolbarClick();
    }

    private void RestoreMainContentFocusAfterToolbarClick()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsActive)
            {
                return;
            }

            if (ShortcutsGrid.IsVisible && ShortcutsGrid.IsEnabled)
            {
                Keyboard.Focus(ShortcutsGrid);
                return;
            }

            Keyboard.Focus(MainFocusTarget);
        }, DispatcherPriority.Background);
    }

    private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.FocusedElement is not DependencyObject focusedElement)
        {
            return;
        }

        if (!IsTextInputElement(focusedElement))
        {
            return;
        }

        if (IsTextInputElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        Keyboard.ClearFocus();
        Keyboard.Focus(MainFocusTarget);
    }

    private void LogWindowInputDiagnostic(string eventName, string details = "")
    {
        var state = $"active={IsActive} visible={IsVisible} state={WindowState} "
            + $"focused={DescribeElement(Keyboard.FocusedElement)} modifiers={Keyboard.Modifiers}";
        _inputDiagnosticLogService.Log(eventName, $"{state} {details}".Trim());
    }

    private static string DescribeElement(object? element)
    {
        return element is null ? "null" : element.GetType().Name;
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
        if (ShouldRestoreMinimizedFactoryMapOnToggle(
                _isFactoryMapOpen,
                _factoryMapWindow is not null,
                _factoryMapWindow?.WindowState ?? WindowState.Normal))
        {
            RestoreMinimizedFactoryMapWindow();
            return;
        }

        if (_isFactoryMapOpen)
        {
            CloseFactoryMapByUserAction();
            return;
        }

        _isFactoryMapOpen = true;
        _runtimeLayoutState.WasFactoryMapOpen = true;
        ShowFactoryMapIfNeeded();
        ActivateFactoryMapWindow();
    }

    private void ShowFactoryMapIfNeeded()
    {
        if (!_isFactoryMapOpen)
        {
            return;
        }

        if (_factoryMapWindow is null)
        {
            _factoryMapWindow = new FactoryMapWindow(
                SelectShortcutFromMap,
                GetContextMenuCapabilitiesForMap,
                ExecuteContextMenuCapabilityFromMapAsync,
                EditShortcutFromMap,
                DeleteShortcutFromMap,
                SaveFactoryMapLayout,
                GetCurrentShortcutsForMap,
                ResolveFactoryMapLayoutPath,
                DownloadAdminUiLinksFromMap,
                MarkMapFileUsed)
            {
                DataContext = DataContext
            };
            _factoryMapWindow.ViewStateChanged += FactoryMapWindow_ViewStateChanged;
            _factoryMapWindow.CloseRequested += (_, _) => CloseFactoryMapByUserAction();
            _factoryMapWindow.LocationChanged += FactoryMapWindow_LocationOrSizeChanged;
            _factoryMapWindow.SizeChanged += FactoryMapWindow_LocationOrSizeChanged;
            _factoryMapWindow.StateChanged += FactoryMapWindow_StateChanged;
            _factoryMapWindow.Closing += FactoryMapWindow_Closing;
            _factoryMapWindow.Closed += (_, _) =>
            {
                _isFactoryMapOpen = false;
                _runtimeLayoutState.WasFactoryMapOpen = false;
                _factoryMapWindow = null;
            };
        }

        PositionFactoryMapWindow(useSessionBounds: true);
        _factoryMapWindow.Show();

        RefreshFactoryMap();
        RestoreFactoryMapWindowState();
    }

    private void ToggleFactoryMapFromGlobalHotkey()
    {
        LogWindowInputDiagnostic("MapHotkeyCallback", "configured=Alt+X");
        Dispatcher.Invoke(() =>
        {
            var action = GetFactoryMapHotkeyAction(
                _factoryMapWindow is not null,
                _factoryMapWindow?.WindowState == WindowState.Minimized,
                _factoryMapWindow is { IsVisible: true, WindowState: not WindowState.Minimized, IsActive: true },
                IsFactoryMapHotkeyBlocked());

            switch (action)
            {
                case FactoryMapHotkeyAction.Open:
                    _isFactoryMapOpen = true;
                    _runtimeLayoutState.WasFactoryMapOpen = true;
                    ShowFactoryMapIfNeeded();
                    ActivateFactoryMapWindow();
                    break;
                case FactoryMapHotkeyAction.Restore:
                    RestoreMinimizedFactoryMapWindow();
                    break;
                case FactoryMapHotkeyAction.Activate:
                    _factoryMapWindow?.Show();
                    ActivateFactoryMapWindow();
                    break;
                case FactoryMapHotkeyAction.Minimize:
                    SaveFactoryMapStateToSession(includeViewState: false);
                    if (_factoryMapWindow is not null)
                    {
                        _factoryMapWindow.WindowState = WindowState.Minimized;
                    }
                    break;
                case FactoryMapHotkeyAction.Ignore:
                default:
                    break;
            }
        });
    }

    private bool IsFactoryMapHotkeyBlocked()
    {
        return _isExitRequested
            || _hasCleanedUpForClose
            || (DataContext is MainViewModel { IsBusy: true })
            || HasActiveBlockingWindow();
    }

    private void RestoreMinimizedFactoryMapWindow()
    {
        if (_factoryMapWindow is null)
        {
            ShowFactoryMapIfNeeded();
            return;
        }

        _isFactoryMapOpen = true;
        _runtimeLayoutState.WasFactoryMapOpen = true;

        _factoryMapWindow.Show();
        _factoryMapWindow.WindowState = WindowState.Normal;
        _runtimeLayoutState.FactoryMapWindowState = FactoryMapWindowStateKinds.Normal;
        SaveWindowLayoutImmediately();
        _factoryMapWindow.Activate();
    }

    private void RestoreFactoryMapWindowState()
    {
        if (_factoryMapWindow is null)
        {
            return;
        }

        _isApplyingRuntimeLayout = true;
        try
        {
            switch (NormalizeFactoryMapWindowState(_runtimeLayoutState.FactoryMapWindowState))
            {
                case FactoryMapWindowStateKinds.WorkspaceMaximized:
                    ModernTitleBar.ApplyWorkspaceMaximized(_factoryMapWindow);
                    break;
                case FactoryMapWindowStateKinds.Minimized:
                    _factoryMapWindow.WindowState = WindowState.Minimized;
                    break;
                case FactoryMapWindowStateKinds.Normal:
                default:
                    if (_factoryMapWindow.WindowState == WindowState.Minimized)
                    {
                        _factoryMapWindow.WindowState = WindowState.Normal;
                    }

                    break;
            }
        }
        finally
        {
            _isApplyingRuntimeLayout = false;
        }
    }

    private bool HasActiveBlockingWindow()
    {
        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            if (window == this || window == _factoryMapWindow || !window.IsVisible || !window.IsActive)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsTextInputElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.TextBox
                or PasswordBox
                or System.Windows.Controls.RichTextBox)
            {
                return true;
            }

            if (source is System.Windows.Controls.ComboBox { IsEditable: true })
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
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

        if (loadResult.Map.RequiresPersistence)
        {
            var migrationSave = _factoryMapLayoutService.Save(layoutPath, loadResult.Map);
            if (!migrationSave.Success)
            {
                _factoryMapWindow.ShowError(migrationSave.ErrorMessage ?? "工厂地图节点尺寸迁移后保存失败。");
                return;
            }
        }

        var hasViewState = _runtimeLayoutState.FactoryMapView is not null;
        _factoryMapWindow.RenderMap(loadResult.Map, resetView: !hasViewState);
        if (hasViewState)
        {
            _factoryMapWindow.RestoreViewState(_runtimeLayoutState.FactoryMapView);
        }

        SyncFactoryMapSelection();
        _factoryMapWindow.RestoreMapInputFocus();
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
        viewModel.WorkspaceLayoutImported -= MainViewModel_WorkspaceLayoutImported;
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
        if (_factoryMapWindow is null)
        {
            return;
        }

        if (ShouldRestoreFactoryMapBounds(useSessionBounds, _runtimeLayoutState.HasFactoryMapBounds))
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

        if (WindowState == WindowState.Minimized)
        {
            ApplyWindowBounds(_factoryMapWindow, CalculateDefaultFactoryMapBoundsWithoutMainWindow());
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

    private Rect CalculateDefaultFactoryMapBoundsWithoutMainWindow()
    {
        if (_factoryMapWindow is null)
        {
            return Rect.Empty;
        }

        var workArea = GetWorkArea();
        var width = Clamp(workArea.Width * 0.55, _factoryMapWindow.MinWidth, workArea.Width);
        var height = Clamp(workArea.Height * DefaultLayoutMapHeightRatio, _factoryMapWindow.MinHeight, workArea.Height * 0.90);
        var left = workArea.Left + (workArea.Width - width) / 2;
        var top = workArea.Top + (workArea.Height - height) / 2;
        return ClampBoundsToWorkArea(
            new Rect(left, top, width, height),
            _factoryMapWindow.MinWidth,
            _factoryMapWindow.MinHeight);
    }

    private void CloseFactoryMapByUserAction()
    {
        if (_isClosingFactoryMapByOwner)
        {
            return;
        }

        if (_factoryMapWindow is null)
        {
            _isFactoryMapOpen = false;
            _runtimeLayoutState.WasFactoryMapOpen = false;
            SaveWindowLayoutImmediately();
            return;
        }

        _isClosingFactoryMapByOwner = true;
        try
        {
            SaveFactoryMapStateToSession();
            _isFactoryMapOpen = false;
            _runtimeLayoutState.WasFactoryMapOpen = false;
            SaveWindowLayoutImmediately();
            _factoryMapWindow.Close();
        }
        finally
        {
            _isClosingFactoryMapByOwner = false;
        }
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
        SaveWindowLayoutImmediately();
        _isClosingFactoryMapByOwner = true;
        try
        {
            _factoryMapWindow.Close();
            _factoryMapWindow = null;
        }
        finally
        {
            _isClosingFactoryMapByOwner = false;
        }
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
        LogWindowInputDiagnostic("StateChanged");
        if (WindowState == WindowState.Minimized)
        {
            SaveMainWindowPresentationStateToSession();
        }
    }

    private void MainTitleBar_WorkspaceMaximizedChanged(object? sender, EventArgs e)
    {
        SaveMainWindowPresentationStateToSession();
        SaveWindowLayoutImmediately();
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        _factoryMapWindow?.CancelPendingInputFocusRestore();
        LogWindowInputDiagnostic("Activated");
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        LogWindowInputDiagnostic("Deactivated");
    }

    private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        LogWindowInputDiagnostic("VisibilityChanged", $"visible={e.NewValue}");
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var diagnosticKey = e.Key == Key.System ? e.SystemKey : e.Key;
        if (diagnosticKey is not (Key.Enter or Key.Back))
        {
            return;
        }

        LogWindowInputDiagnostic(
            "PreviewKeyDown",
            $"key={e.Key} logicalKey={diagnosticKey} systemKey={e.SystemKey} modifiers={Keyboard.Modifiers} handled={e.Handled} "
            + $"originalSource={DescribeElement(e.OriginalSource)}");
    }

    private void FactoryMapWindow_StateChanged(object? sender, EventArgs e)
    {
        if (sender is not FactoryMapWindow)
        {
            return;
        }

        SaveFactoryMapStateToSession(includeViewState: false);
    }

    private void FactoryMapWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosingFactoryMapByOwner || sender is not FactoryMapWindow)
        {
            return;
        }

        SaveFactoryMapStateToSession();
        _isFactoryMapOpen = false;
        _runtimeLayoutState.WasFactoryMapOpen = false;
        SaveWindowLayoutImmediately();
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
        if (!_initialLayoutRestoreGuard.CanSaveWindowBounds
            || _isApplyingRuntimeLayout
            || WindowState != WindowState.Normal
            || ModernTitleBar.IsWorkspaceMaximized(this))
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

    private void SaveMainWindowPresentationStateToSession()
    {
        if (!_initialLayoutRestoreGuard.CanSaveWindowBounds || _isApplyingRuntimeLayout)
        {
            return;
        }

        _runtimeLayoutState.MainWindowState = ModernTitleBar.IsWorkspaceMaximized(this)
            ? MainWindowStateKinds.WorkspaceMaximized
            : MainWindowStateKinds.Normal;

        if (_runtimeLayoutState.MainWindowState == MainWindowStateKinds.Normal
            && WindowState == WindowState.Normal)
        {
            SaveMainWindowBoundsToSession();
            return;
        }

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

    private void RestoreMainWindowPresentationState()
    {
        if (NormalizeMainWindowState(_runtimeLayoutState.MainWindowState)
            != MainWindowStateKinds.WorkspaceMaximized)
        {
            return;
        }

        _isApplyingRuntimeLayout = true;
        try
        {
            ModernTitleBar.ApplyWorkspaceMaximized(this);
        }
        finally
        {
            _isApplyingRuntimeLayout = false;
        }
    }

    private void SaveFactoryMapStateToSession(bool includeViewState = true)
    {
        if (_factoryMapWindow is null)
        {
            _runtimeLayoutState.WasFactoryMapOpen = _isFactoryMapOpen;
            return;
        }

        _runtimeLayoutState.WasFactoryMapOpen = _isFactoryMapOpen;
        if (IsFactoryMapEffectivelyWorkspaceMaximized())
        {
            _runtimeLayoutState.FactoryMapWindowState = FactoryMapWindowStateKinds.WorkspaceMaximized;
            SaveFactoryMapBoundsToSession(CaptureFactoryMapWindowBounds());
            if (includeViewState && _factoryMapWindow.HasUserViewState)
            {
                _runtimeLayoutState.FactoryMapView = _factoryMapWindow.CaptureViewState();
            }

            ScheduleWindowLayoutSave();
            return;
        }

        _runtimeLayoutState.FactoryMapWindowState = _factoryMapWindow.WindowState == WindowState.Minimized
            ? FactoryMapWindowStateKinds.Minimized
            : FactoryMapWindowStateKinds.Normal;

        SaveFactoryMapBoundsToSession(CaptureFactoryMapWindowBounds());

        if (includeViewState && _factoryMapWindow.HasUserViewState)
        {
            _runtimeLayoutState.FactoryMapView = _factoryMapWindow.CaptureViewState();
        }

        ScheduleWindowLayoutSave();
    }

    private Rect CaptureFactoryMapWindowBounds()
    {
        if (_factoryMapWindow is null)
        {
            return Rect.Empty;
        }

        return _factoryMapWindow.WindowState == WindowState.Minimized
            ? _factoryMapWindow.RestoreBounds
            : new Rect(
                _factoryMapWindow.Left,
                _factoryMapWindow.Top,
                _factoryMapWindow.ActualWidth > 0 ? _factoryMapWindow.ActualWidth : _factoryMapWindow.Width,
                _factoryMapWindow.ActualHeight > 0 ? _factoryMapWindow.ActualHeight : _factoryMapWindow.Height);
    }

    private void SaveFactoryMapBoundsToSession(Rect sourceBounds)
    {
        var width = sourceBounds.Width;
        var height = sourceBounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _runtimeLayoutState.HasFactoryMapBounds = true;
        _runtimeLayoutState.FactoryMapLeft = sourceBounds.Left;
        _runtimeLayoutState.FactoryMapTop = sourceBounds.Top;
        _runtimeLayoutState.FactoryMapWidth = width;
        _runtimeLayoutState.FactoryMapHeight = height;
    }

    private bool IsFactoryMapEffectivelyWorkspaceMaximized()
    {
        return _factoryMapWindow is not null
            && (ModernTitleBar.IsWorkspaceMaximized(_factoryMapWindow)
                || IsBoundsEffectivelyWorkspaceMaximized(CaptureFactoryMapWindowBounds()));
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
            MainWindowState = _runtimeLayoutState.MainWindowState,
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
            FactoryMapWindowState = _runtimeLayoutState.FactoryMapWindowState,
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

        _runtimeLayoutState.MainWindowState = NormalizeMainWindowState(config.MainWindowState);

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
        _runtimeLayoutState.FactoryMapWindowState = NormalizeFactoryMapWindowState(config.FactoryMapWindowState);
        if (_runtimeLayoutState.FactoryMapWindowState == FactoryMapWindowStateKinds.Normal
            && config.FactoryMapWindow is not null
            && IsBoundsEffectivelyWorkspaceMaximized(ToRect(config.FactoryMapWindow)))
        {
            _runtimeLayoutState.FactoryMapWindowState = FactoryMapWindowStateKinds.WorkspaceMaximized;
        }

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

    private static string NormalizeFactoryMapWindowState(string? state)
    {
        return state switch
        {
            FactoryMapWindowStateKinds.Minimized => FactoryMapWindowStateKinds.Minimized,
            FactoryMapWindowStateKinds.WorkspaceMaximized => FactoryMapWindowStateKinds.WorkspaceMaximized,
            _ => FactoryMapWindowStateKinds.Normal
        };
    }

    internal static string NormalizeMainWindowState(string? state)
    {
        return string.Equals(state, MainWindowStateKinds.WorkspaceMaximized, StringComparison.Ordinal)
            ? MainWindowStateKinds.WorkspaceMaximized
            : MainWindowStateKinds.Normal;
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

        column.Width = new DataGridLength(width.Value, DataGridLengthUnitType.Star);
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

    private static bool IsBoundsEffectivelyWorkspaceMaximized(Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        var workArea = GetWorkArea();
        return Math.Abs(bounds.Left - workArea.Left) <= WorkspaceMaximizedBoundsTolerance
            && Math.Abs(bounds.Top - workArea.Top) <= WorkspaceMaximizedBoundsTolerance
            && Math.Abs(bounds.Width - workArea.Width) <= WorkspaceMaximizedBoundsTolerance
            && Math.Abs(bounds.Height - workArea.Height) <= WorkspaceMaximizedBoundsTolerance;
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
                SelectShortcutInGrid(viewModel, shortcut, focusGrid: false);
            }), DispatcherPriority.Background);
            return;
        }

        SelectShortcutInGrid(viewModel, shortcut, focusGrid: false);
    }

    private IReadOnlyList<ContextMenuCapabilityDefinition> GetContextMenuCapabilitiesForMap(string surface)
    {
        return DataContext is MainViewModel viewModel
            ? viewModel.GetContextMenuCapabilities(surface)
            : [];
    }

    private async Task ExecuteContextMenuCapabilityFromMapAsync(
        VSLoader.Models.ShortcutItem shortcut,
        ContextMenuCapabilityDefinition capability)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.ExecuteContextMenuCapabilityAsync(
                capability,
                shortcut,
                ContextMenuCapabilitySurfaces.FactoryMap);
        }
    }

    private void EditShortcutFromMap(VSLoader.Models.ShortcutItem shortcut)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        try
        {
            var editViewModel = new ShortcutEditViewModel(viewModel.Shortcuts, shortcut, new DialogService());
            Window owner = _factoryMapWindow is { IsVisible: true } ? _factoryMapWindow : this;
            var window = new ShortcutEditWindow(editViewModel, owner);

            if (window.ShowDialog() == true && editViewModel.Result is not null)
            {
                viewModel.ApplyEditedShortcutFromMap(shortcut, editViewModel.Result);
                RefreshFactoryMap();
            }
        }
        catch (Exception ex)
        {
            if (_factoryMapWindow is { IsVisible: true })
            {
                _factoryMapWindow.ShowError($"编辑快捷项失败：{ex.Message}");
            }
            else
            {
                new DialogService().ShowError($"编辑快捷项失败：{ex.Message}");
            }
        }
    }

    private void DeleteShortcutFromMap(VSLoader.Models.ShortcutItem shortcut)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.DeleteShortcutFromMap(shortcut);
        RefreshFactoryMap();
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

        try
        {
            viewModel.BusyOverlayHost = BusyOverlayHost.Map;
            await viewModel.DownloadAdminUiLinksCommand.ExecuteAsync(null);
        }
        finally
        {
            viewModel.BusyOverlayHost = BusyOverlayHost.Main;
        }
    }

    private void SelectShortcutInGrid(MainViewModel viewModel, VSLoader.Models.ShortcutItem shortcut, bool focusGrid)
    {
        viewModel.SelectedShortcut = shortcut;
        ShortcutsGrid.SelectedItem = shortcut;
        ShortcutsGrid.ScrollIntoView(shortcut);
        if (focusGrid)
        {
            ShortcutsGrid.Focus();
        }
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

    private async void ExitApplication()
    {
        await Dispatcher.InvokeAsync(ExitApplicationAsync);
    }

    private async Task ExitApplicationAsync()
    {
        if (!ShouldBeginShutdown(_isShutdownInProgress))
        {
            return;
        }

        _isShutdownInProgress = true;
        _isExitRequested = true;
        CleanupForClose();

        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.StopUpdateCheckLoopAsync(TimeSpan.FromSeconds(3));
        }

        System.Windows.Application.Current.Shutdown();
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

    private void ShortcutContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu
            || DataContext is not MainViewModel viewModel
            || viewModel.SelectedShortcut is not { } shortcut)
        {
            return;
        }

        menu.Items.Clear();
        var capabilities = viewModel.GetContextMenuCapabilities(ContextMenuCapabilitySurfaces.ShortcutList);
        foreach (var capability in capabilities)
        {
            var item = new MenuItem
            {
                Header = capability.Name,
                Style = (Style)FindResource("ModernMenuItemStyle"),
                IsEnabled = !viewModel.IsBusy,
                Tag = capability
            };
            item.Click += async (_, _) =>
            {
                await viewModel.ExecuteContextMenuCapabilityAsync(
                    capability,
                    shortcut,
                    ContextMenuCapabilitySurfaces.ShortcutList);
            };
            menu.Items.Add(item);
        }

        if (capabilities.Count > 0)
        {
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(new MenuItem
        {
            Header = "编辑",
            Style = (Style)FindResource("ModernMenuItemStyle"),
            Command = viewModel.EditShortcutCommand
        });
        menu.Items.Add(new MenuItem
        {
            Header = "删除",
            Style = (Style)FindResource("ModernDangerMenuItemStyle"),
            Command = viewModel.DeleteShortcutCommand
        });
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
