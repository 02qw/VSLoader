using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
        PreviewKeyDown += SettingsWindow_PreviewKeyDown;
        PreviewMouseDown += SettingsWindow_PreviewMouseDown;
        viewModel.EditContextMenuCapability = definition =>
        {
            var editorViewModel = new ContextMenuCapabilityEditorViewModel(definition, new DialogService());
            var editorWindow = new ContextMenuCapabilityEditorWindow(editorViewModel, this);
            return editorWindow.ShowDialog() == true ? editorViewModel.Result : null;
        };
        viewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
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

        var scrollViewer = FindAncestorScrollViewer(sender as DependencyObject);
        if (scrollViewer is null)
        {
            return;
        }

        var targetOffset = CalculateWheelScrollOffset(
            scrollViewer.VerticalOffset,
            e.Delta,
            scrollViewer.ScrollableHeight);

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private static System.Windows.Controls.ScrollViewer? FindAncestorScrollViewer(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is System.Windows.Controls.ScrollViewer scrollViewer
                && string.Equals(scrollViewer.Tag as string, "SettingsPage", StringComparison.Ordinal))
            {
                return scrollViewer;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void SettingsWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (viewModel.IsRecordingMapHotkey)
        {
            e.Handled = true;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
                or Key.System or Key.None)
            {
                return;
            }

            if (MapHotkeyService.IsSupportedKey(key))
            {
                var mapCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                var mapAlt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
                var mapShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                viewModel.SetRecordedMapHotkey(mapCtrl, mapAlt, mapShift, MapHotkeyService.GetDisplayKey(key));
            }

            return;
        }

        if (!viewModel.IsRecordingHotkey)
        {
            return;
        }

        e.Handled = true;
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
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsRecordingMapHotkey)
        {
            e.Handled = e.ChangedButton is MouseButton.XButton1 or MouseButton.XButton2;
            return;
        }

        if (!viewModel.IsRecordingHotkey)
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
