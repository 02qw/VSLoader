using System.Windows;
using VSLoader.ViewModels;

namespace VSLoader.Views;

public partial class ShortcutEditWindow : Window
{
    public ShortcutEditWindow(ShortcutEditViewModel viewModel, Window? owner = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        Owner = owner ?? System.Windows.Application.Current.MainWindow;
        viewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }
}
