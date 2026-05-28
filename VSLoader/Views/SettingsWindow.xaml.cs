using System.Windows;
using VSLoader.ViewModels;

namespace VSLoader.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Owner = System.Windows.Application.Current.MainWindow;
        viewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }
}
