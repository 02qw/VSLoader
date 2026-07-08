using System.Windows;

namespace VSLoader.Views.Controls;

public partial class ModernTitleBar : System.Windows.Controls.UserControl
{
    public event EventHandler? CloseRequested;

    public ModernTitleBar()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UpdateMaximizeButtonVisibility();
            UpdateMaximizeRestoreIcon();

            if (OwnerWindow is { } window)
            {
                window.StateChanged -= OwnerWindow_StateChanged;
                window.StateChanged += OwnerWindow_StateChanged;
            }
        };
        Unloaded += (_, _) =>
        {
            if (OwnerWindow is { } window)
            {
                window.StateChanged -= OwnerWindow_StateChanged;
            }
        };
    }

    private Window? OwnerWindow => Window.GetWindow(this);

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (OwnerWindow is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } window)
        {
            return;
        }

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

        UpdateMaximizeRestoreIcon();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (CloseRequested is not null)
        {
            CloseRequested.Invoke(this, EventArgs.Empty);
            return;
        }

        OwnerWindow?.Close();
    }

    private void UpdateMaximizeButtonVisibility()
    {
        if (OwnerWindow is not { } window)
        {
            return;
        }

        MaximizeRestoreButton.Visibility = window.ResizeMode == ResizeMode.NoResize
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OwnerWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeRestoreIcon();
    }

    private void UpdateMaximizeRestoreIcon()
    {
        if (OwnerWindow is not { } window)
        {
            return;
        }

        var isMaximized = window.WindowState == WindowState.Maximized;
        MaximizeIcon.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;
    }
}
