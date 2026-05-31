using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSLoader.Services;
using VSLoader.ViewModels;
using WinForms = System.Windows.Forms;

namespace VSLoader;

public partial class MainWindow : Window
{
    private static readonly Dictionary<string, string> SortHeaderTitles = new()
    {
        ["Name"] = "名称",
        ["Description"] = "备注",
        ["UpdatedAt"] = "更新时间"
    };

    private readonly GlobalHotkeyService _hotkeyService = new();
    private WinForms.NotifyIcon? _notifyIcon;
    private bool _isExitRequested;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();
        SetInitialSortState();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
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

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _hotkeyService.Dispose();
        DisposeTrayIcon();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        e.Cancel = true;
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
        });
    }

    private void InitializeTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "tomato.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconPath),
            Text = "VSLoader",
            Visible = true,
            ContextMenuStrip = CreateTrayMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => RestoreAndActivate();
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
