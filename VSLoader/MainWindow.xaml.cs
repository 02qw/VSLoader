using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSLoader.Services;
using VSLoader.ViewModels;
using VSLoader.Views;
using WinForms = System.Windows.Forms;

namespace VSLoader;

public partial class MainWindow : Window
{
    private const double DefaultLayoutLeftRatio = 0.01;
    private const double DefaultLayoutTopRatio = 0.055;
    private const double DefaultLayoutMainWidthRatio = 0.50;
    private const double DefaultLayoutMainHeightRatio = 0.70;
    private const double DefaultLayoutRightMarginRatio = 0.01;
    private const double DefaultLayoutGap = 0;

    private static readonly Dictionary<string, string> SortHeaderTitles = new()
    {
        ["Name"] = "名称",
        ["Description"] = "备注",
        ["UpdatedAt"] = "更新时间"
    };

    private readonly GlobalHotkeyService _hotkeyService = new();
    private readonly FactoryMapLayoutService _factoryMapLayoutService = new();
    private WinForms.NotifyIcon? _notifyIcon;
    private FactoryMapWindow? _factoryMapWindow;
    private bool _isFactoryMapOpen;
    private bool _isExitRequested;
    private bool _hasAppliedDefaultLayout;
    private bool _isViewModelEventsAttached;

    public MainWindow()
    {
        InitializeComponent();
        Title = BuildWindowTitle();
        InitializeTrayIcon();
        SetInitialSortState();
        Loaded += MainWindow_Loaded;
        LocationChanged += MainWindow_LocationOrSizeChanged;
        SizeChanged += MainWindow_LocationOrSizeChanged;
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private static string BuildWindowTitle()
    {
        return FormatWindowTitle(Assembly.GetExecutingAssembly().GetName().Version);
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

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyDefaultWindowLayoutOnce();

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
    }

    private void ApplyDefaultWindowLayoutOnce()
    {
        if (_hasAppliedDefaultLayout || WindowState != WindowState.Normal)
        {
            return;
        }

        _hasAppliedDefaultLayout = true;
        ApplyDefaultMainWindowLayout();
    }

    private void ApplyDefaultMainWindowLayout()
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

        Width = targetWidth;
        Height = targetHeight;
        Left = Math.Max(workArea.Left, targetLeft);
        Top = Math.Max(workArea.Top, targetTop);
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
        CloseFactoryMapForExit();
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
        HideFactoryMapWindow();
        Hide();
    }

    private void ToggleWindowFromHotkey()
    {
        if (!IsVisible || WindowState == WindowState.Minimized)
        {
            RestoreAndActivate();
            return;
        }

        if (IsActive)
        {
            WindowState = WindowState.Minimized;
            return;
        }

        RestoreAndActivate();
    }

    private void RestoreAndActivate()
    {
        Dispatcher.Invoke(() =>
        {
            Show();

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

    private void ToggleFactoryMapWindow()
    {
        if (_factoryMapWindow is { IsVisible: true })
        {
            _isFactoryMapOpen = false;
            _factoryMapWindow.Close();
            _factoryMapWindow = null;
            return;
        }

        _isFactoryMapOpen = true;
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
                SaveFactoryMapLayout,
                GetCurrentShortcutsForMap,
                ResolveFactoryMapLayoutPath)
            {
                Owner = this
            };
            _factoryMapWindow.Closed += (_, _) =>
            {
                _factoryMapWindow = null;
            };
        }

        PositionFactoryMapWindow();
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

        _factoryMapWindow.RenderMap(loadResult.Map);
        SyncFactoryMapSelection();
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

    private static string ResolveFactoryMapLayoutPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(appData, "VSLoader", "factory-map.layout.json");
    }

    private static Rect GetWorkArea()
    {
        return SystemParameters.WorkArea;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    private void PositionFactoryMapWindow()
    {
        if (_factoryMapWindow is null || WindowState == WindowState.Minimized)
        {
            return;
        }

        var workArea = GetWorkArea();
        var rightMargin = workArea.Width * DefaultLayoutRightMarginRatio;
        var mainWidth = ActualWidth > 0 ? ActualWidth : Width;
        var mainHeight = ActualHeight > 0 ? ActualHeight : Height;
        var mapLeft = Left + mainWidth + DefaultLayoutGap;
        var mapTop = Top;
        var mapHeight = Clamp(mainHeight, _factoryMapWindow.MinHeight, workArea.Height * 0.90);
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

        _factoryMapWindow.Left = Math.Max(workArea.Left, mapLeft);
        _factoryMapWindow.Top = Math.Max(workArea.Top, mapTop);
        _factoryMapWindow.Width = mapWidth;
        _factoryMapWindow.Height = mapHeight;
    }

    private void HideFactoryMapWindow()
    {
        _factoryMapWindow?.Hide();
    }

    private void CloseFactoryMapForExit()
    {
        if (_factoryMapWindow is null)
        {
            return;
        }

        _isFactoryMapOpen = false;
        _factoryMapWindow.Close();
        _factoryMapWindow = null;
    }

    private void MainWindow_LocationOrSizeChanged(object? sender, EventArgs e)
    {
        PositionFactoryMapWindow();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideFactoryMapWindow();
            return;
        }

        ShowFactoryMapIfNeeded();
    }

    private void SelectShortcutFromMap(VSLoader.Models.ShortcutItem shortcut)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

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
            "UpdatedAt" => ShortcutSortField.UpdatedAt,
            _ => default
        };

        return sortMemberPath is "Name" or "Description" or "UpdatedAt";
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
