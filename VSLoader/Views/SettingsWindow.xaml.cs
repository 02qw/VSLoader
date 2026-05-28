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
        AdminUiPasswordBox.Password = viewModel.AdminUiPassword;
        viewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }

    private void AdminUiPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.AdminUiPassword = AdminUiPasswordBox.Password;
        }
    }
}
