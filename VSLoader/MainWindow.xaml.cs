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

    public MainWindow()
    {
        InitializeComponent();
        SetInitialSortState();
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
