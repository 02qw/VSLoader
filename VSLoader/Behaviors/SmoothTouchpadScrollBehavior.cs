using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace VSLoader.Behaviors;

public static class SmoothTouchpadScrollBehavior
{
    private const int WM_MOUSEHWHEEL = 0x020E;
    private const double DefaultSensitivity = 1.0;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SmoothTouchpadScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty EnableHorizontalProperty =
        DependencyProperty.RegisterAttached(
            "EnableHorizontal",
            typeof(bool),
            typeof(SmoothTouchpadScrollBehavior),
            new PropertyMetadata(true));

    public static readonly DependencyProperty EnableVerticalProperty =
        DependencyProperty.RegisterAttached(
            "EnableVertical",
            typeof(bool),
            typeof(SmoothTouchpadScrollBehavior),
            new PropertyMetadata(true));

    public static readonly DependencyProperty HorizontalSensitivityProperty =
        DependencyProperty.RegisterAttached(
            "HorizontalSensitivity",
            typeof(double),
            typeof(SmoothTouchpadScrollBehavior),
            new PropertyMetadata(DefaultSensitivity));

    public static readonly DependencyProperty VerticalSensitivityProperty =
        DependencyProperty.RegisterAttached(
            "VerticalSensitivity",
            typeof(double),
            typeof(SmoothTouchpadScrollBehavior),
            new PropertyMetadata(DefaultSensitivity));

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(SmoothTouchpadScrollState),
            typeof(SmoothTouchpadScrollBehavior));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static bool GetEnableHorizontal(DependencyObject obj) => (bool)obj.GetValue(EnableHorizontalProperty);

    public static void SetEnableHorizontal(DependencyObject obj, bool value) => obj.SetValue(EnableHorizontalProperty, value);

    public static bool GetEnableVertical(DependencyObject obj) => (bool)obj.GetValue(EnableVerticalProperty);

    public static void SetEnableVertical(DependencyObject obj, bool value) => obj.SetValue(EnableVerticalProperty, value);

    public static double GetHorizontalSensitivity(DependencyObject obj) => (double)obj.GetValue(HorizontalSensitivityProperty);

    public static void SetHorizontalSensitivity(DependencyObject obj, double value) => obj.SetValue(HorizontalSensitivityProperty, value);

    public static double GetVerticalSensitivity(DependencyObject obj) => (double)obj.GetValue(VerticalSensitivityProperty);

    public static void SetVerticalSensitivity(DependencyObject obj, double value) => obj.SetValue(VerticalSensitivityProperty, value);

    internal static bool IsFineGrainedVerticalDelta(int delta)
    {
        return delta != 0 && Math.Abs(delta) < 120;
    }

    internal static double CalculateTargetOffset(double current, int delta, double sensitivity, double min, double max)
    {
        var target = current - delta * sensitivity;
        return Math.Max(min, Math.Min(target, max));
    }

    internal static double CalculateHorizontalTargetOffset(double current, int delta, double sensitivity, double min, double max)
    {
        var target = current + delta * sensitivity;
        return Math.Max(min, Math.Min(target, max));
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            Attach(element);
            return;
        }

        Detach(element);
    }

    private static void Attach(FrameworkElement element)
    {
        if (element.GetValue(StateProperty) is SmoothTouchpadScrollState)
        {
            return;
        }

        var state = new SmoothTouchpadScrollState(element);
        element.SetValue(StateProperty, state);
        element.Loaded += Element_Loaded;
        element.Unloaded += Element_Unloaded;
        element.PreviewMouseWheel += Element_PreviewMouseWheel;

        if (element.IsLoaded)
        {
            state.AttachHook();
        }
    }

    private static void Detach(FrameworkElement element)
    {
        if (element.GetValue(StateProperty) is not SmoothTouchpadScrollState state)
        {
            return;
        }

        element.Loaded -= Element_Loaded;
        element.Unloaded -= Element_Unloaded;
        element.PreviewMouseWheel -= Element_PreviewMouseWheel;
        state.DetachHook();
        element.ClearValue(StateProperty);
    }

    private static void Element_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element
            && element.GetValue(StateProperty) is SmoothTouchpadScrollState state)
        {
            state.AttachHook();
        }
    }

    private static void Element_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element
            && element.GetValue(StateProperty) is SmoothTouchpadScrollState state)
        {
            state.DetachHook();
        }
    }

    private static void Element_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not FrameworkElement element
            || !GetEnableVertical(element)
            || !IsFineGrainedVerticalDelta(e.Delta)
            || FindScrollViewer(element) is not { } scrollViewer
            || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        var targetOffset = CalculateTargetOffset(
            scrollViewer.VerticalOffset,
            e.Delta,
            GetVerticalSensitivity(element),
            0,
            scrollViewer.ScrollableHeight);

        if (Math.Abs(targetOffset - scrollViewer.VerticalOffset) < 0.001)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private static bool TryHandleHorizontalWheel(FrameworkElement element, nint wParam)
    {
        if (!GetEnableHorizontal(element)
            || !IsMouseOverElement(element)
            || FindScrollViewer(element) is not { } scrollViewer
            || scrollViewer.ScrollableWidth <= 0)
        {
            return false;
        }

        var delta = GetWheelDelta(wParam);
        if (delta == 0)
        {
            return false;
        }

        var targetOffset = CalculateHorizontalTargetOffset(
            scrollViewer.HorizontalOffset,
            delta,
            GetHorizontalSensitivity(element),
            0,
            scrollViewer.ScrollableWidth);

        if (Math.Abs(targetOffset - scrollViewer.HorizontalOffset) < 0.001)
        {
            return false;
        }

        scrollViewer.ScrollToHorizontalOffset(targetOffset);
        return true;
    }

    private static short GetWheelDelta(nint wParam)
    {
        return unchecked((short)((wParam.ToInt64() >> 16) & 0xffff));
    }

    private static bool IsMouseOverElement(FrameworkElement element)
    {
        if (!element.IsVisible)
        {
            return false;
        }

        var point = Mouse.GetPosition(element);
        return point.X >= 0
            && point.Y >= 0
            && point.X <= element.ActualWidth
            && point.Y <= element.ActualHeight;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject source)
    {
        if (source is ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(source);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(source, i);
            var result = FindScrollViewer(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private sealed class SmoothTouchpadScrollState(FrameworkElement element)
    {
        private HwndSource? hwndSource;

        public void AttachHook()
        {
            if (hwndSource is not null)
            {
                return;
            }

            hwndSource = PresentationSource.FromVisual(element) as HwndSource;
            hwndSource?.AddHook(WndProc);
        }

        public void DetachHook()
        {
            if (hwndSource is null)
            {
                return;
            }

            hwndSource.RemoveHook(WndProc);
            hwndSource = null;
        }

        private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL && TryHandleHorizontalWheel(element, wParam))
            {
                handled = true;
            }

            return nint.Zero;
        }
    }
}
