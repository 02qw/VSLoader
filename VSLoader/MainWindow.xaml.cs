using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSLoader.ViewModels;

namespace VSLoader;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
