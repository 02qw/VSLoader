using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using VSLoader.Services;
using VSLoader.ViewModels;

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

    public MainWindow()
    {
        InitializeComponent();
        SetInitialSortState();
        Loaded += MainWindow_Loaded;
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
    }

    private void ToggleWindowFromHotkey()
    {
        if (WindowState == WindowState.Minimized)
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
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
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
}
