using System.Windows;
using System.Windows.Input;
using VSLoader.Services;
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
        PreviewKeyDown += SettingsWindow_PreviewKeyDown;
        PreviewMouseDown += SettingsWindow_PreviewMouseDown;
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

    internal static double CalculateWheelScrollOffset(double currentOffset, int delta, double scrollableHeight)
    {
        var targetOffset = currentOffset - delta;
        return Math.Max(0, Math.Min(targetOffset, scrollableHeight));
    }

    private void SettingsInput_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0)
        {
            return;
        }

        var targetOffset = CalculateWheelScrollOffset(
            SettingsScrollViewer.VerticalOffset,
            e.Delta,
            SettingsScrollViewer.ScrollableHeight);

        SettingsScrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private void SettingsWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel { IsRecordingHotkey: true } viewModel)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.System or Key.None)
        {
            return;
        }

        var ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        var alt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
        var shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        viewModel.SetRecordedHotkey(ctrl, alt, shift, GlobalHotkeyService.GetDisplayKey(key));
    }

    private void SettingsWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not SettingsViewModel { IsRecordingHotkey: true } viewModel)
        {
            return;
        }

        var key = e.ChangedButton switch
        {
            MouseButton.XButton1 => "Mouse4",
            MouseButton.XButton2 => "Mouse5",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        e.Handled = true;
        var ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        var alt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
        var shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        viewModel.SetRecordedHotkey(ctrl, alt, shift, key, "Mouse");
    }
}
