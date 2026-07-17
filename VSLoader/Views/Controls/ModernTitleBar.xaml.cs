using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Runtime.InteropServices;
using VSLoader.Services;
using DrawingRectangle = System.Drawing.Rectangle;
using WinForms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace VSLoader.Views.Controls;

public partial class ModernTitleBar : System.Windows.Controls.UserControl
{
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_SETCURSOR = 0x0020;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_MOUSELEAVE = 0x02A3;
    private const int WM_NCMOUSEMOVE = 0x00A0;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCLBUTTONUP = 0x00A2;
    private const int WM_NCMOUSELEAVE = 0x02A2;
    private const int NativeMouseLogThrottleMilliseconds = 220;
    private const int WpfMouseMoveLogThrottleMilliseconds = 250;
    private static readonly bool EnableTitleBarHoverDebugLogging = false;

    private static readonly string TitleBarHoverDebugLogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VSLoader",
        "titlebar-hover.debug.log");

    private static readonly DependencyProperty IsWorkspaceMaximizedProperty =
        DependencyProperty.RegisterAttached(
            "IsWorkspaceMaximized",
            typeof(bool),
            typeof(ModernTitleBar),
            new PropertyMetadata(false));

    private Rect? normalWindowBounds;
    private bool isApplyingWorkspaceBounds;
    private Window? subscribedOwnerWindow;
    private HwndSource? subscribedHwndSource;
    private long lastNativeMouseLogTicks;
    private long lastWpfMouseMoveLogTicks;
    private string? lastNativeHitLogKey;

    public event EventHandler? CloseRequested;

    public ModernTitleBar()
    {
        InitializeComponent();
        Loaded += ModernTitleBar_Loaded;
        Unloaded += ModernTitleBar_Unloaded;

        if (EnableTitleBarHoverDebugLogging)
        {
            RegisterButtonDiagnostics(MinimizeButton);
            RegisterButtonDiagnostics(MaximizeRestoreButton);
            RegisterButtonDiagnostics(CloseButton);
        }
    }

    private Window? OwnerWindow => Window.GetWindow(this);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    public static bool IsWorkspaceMaximized(Window? window)
    {
        return window?.GetValue(IsWorkspaceMaximizedProperty) is true;
    }

    public static void ApplyWorkspaceMaximized(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        var workArea = ConvertScreenBoundsToDip(window, GetCurrentScreenWorkingArea(window));
        SetIsWorkspaceMaximized(window, true);
        if (window.WindowState == WindowState.Maximized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Left = workArea.Left;
        window.Top = workArea.Top;
        window.Width = workArea.Width;
        window.Height = workArea.Height;
    }

    private static void SetIsWorkspaceMaximized(Window window, bool value)
    {
        window.SetValue(IsWorkspaceMaximizedProperty, value);
    }

    private void ModernTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateMaximizeButtonVisibility();
        UpdateMaximizeRestoreIcon();

        if (OwnerWindow is { } window)
        {
            SubscribeOwnerWindowDiagnostics(window);
        }

        WriteTitleBarHoverDebugLog("TitleBarLoaded", this);
        RefreshMouseHoverState("TitleBarLoaded");
        Dispatcher.BeginInvoke(
            () =>
            {
                RefreshMouseHoverState("TitleBarLoadedIdle");
                WriteTitleBarHoverDebugLog("TitleBarLoadedIdleSnapshot", this);
            },
            System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void ModernTitleBar_Unloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeOwnerWindowDiagnostics();
        WriteTitleBarHoverDebugLog("TitleBarUnloaded", this);
    }

    private void SubscribeOwnerWindowDiagnostics(Window window)
    {
        if (!ReferenceEquals(subscribedOwnerWindow, window))
        {
            UnsubscribeOwnerWindowDiagnostics();
            subscribedOwnerWindow = window;
            window.Activated += OwnerWindow_Activated;
            window.Deactivated += OwnerWindow_Deactivated;
            if (EnableTitleBarHoverDebugLogging)
            {
                window.SourceInitialized += OwnerWindow_SourceInitialized;
                window.LocationChanged += OwnerWindow_LocationChanged;
                window.SizeChanged += OwnerWindow_SizeChanged;
            }
        }

        window.StateChanged -= OwnerWindow_StateChanged;
        window.StateChanged += OwnerWindow_StateChanged;
        if (EnableTitleBarHoverDebugLogging)
        {
            AttachOwnerHwndHook(window);
        }
    }

    private void UnsubscribeOwnerWindowDiagnostics()
    {
        DetachOwnerHwndHook();

        if (subscribedOwnerWindow is not { } window)
        {
            return;
        }

        window.StateChanged -= OwnerWindow_StateChanged;
        window.Activated -= OwnerWindow_Activated;
        window.Deactivated -= OwnerWindow_Deactivated;
        if (EnableTitleBarHoverDebugLogging)
        {
            window.SourceInitialized -= OwnerWindow_SourceInitialized;
            window.LocationChanged -= OwnerWindow_LocationChanged;
            window.SizeChanged -= OwnerWindow_SizeChanged;
        }
        subscribedOwnerWindow = null;
    }

    private void RegisterButtonDiagnostics(WpfButton button)
    {
        button.PreviewMouseMove += TitleBarButton_PreviewMouseMove;
        button.MouseMove += TitleBarButton_MouseMove;
        button.MouseEnter += TitleBarButton_MouseEnter;
        button.MouseLeave += TitleBarButton_MouseLeave;
        button.GotKeyboardFocus += TitleBarButton_GotKeyboardFocus;
        button.LostKeyboardFocus += TitleBarButton_LostKeyboardFocus;
    }

    private void OwnerWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            AttachOwnerHwndHook(window);
            WriteTitleBarHoverDebugLog("OwnerWindowSourceInitialized", sender);
        }
    }

    private void AttachOwnerHwndHook(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            WriteTitleBarHoverDebugLog("NativeWndHookSkipped handle=0", window);
            return;
        }

        var source = HwndSource.FromHwnd(handle);
        if (source is null || ReferenceEquals(subscribedHwndSource, source))
        {
            return;
        }

        DetachOwnerHwndHook();
        subscribedHwndSource = source;
        subscribedHwndSource.AddHook(OwnerWindow_WndProc);
        WriteTitleBarHoverDebugLog($"NativeWndHookAttached hwnd=0x{handle.ToInt64():X}", window);
    }

    private void DetachOwnerHwndHook()
    {
        if (subscribedHwndSource is null)
        {
            return;
        }

        subscribedHwndSource.RemoveHook(OwnerWindow_WndProc);
        subscribedHwndSource = null;
    }

    private IntPtr OwnerWindow_WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var nativeSnapshot = ShouldLogNativeWindowMessage(msg)
            ? BuildNativeMouseSnapshot(hwnd, msg, lParam)
            : null;
        if (nativeSnapshot is not null && ShouldLogNativeWindowMessageNow(msg, nativeSnapshot.HitKey))
        {
            WriteTitleBarHoverDebugLog(
                $"NativeWndProc msg={FormatWindowMessage(msg)} hwnd=0x{hwnd.ToInt64():X} wParam=0x{wParam.ToInt64():X} lParam=0x{lParam.ToInt64():X} handledAtEntry={handled} {nativeSnapshot.Text}",
                subscribedOwnerWindow);
        }

        return IntPtr.Zero;
    }

    private bool ShouldLogNativeWindowMessageNow(int msg, string hitKey)
    {
        if (msg is WM_LBUTTONDOWN or WM_LBUTTONUP or WM_NCLBUTTONDOWN or WM_NCLBUTTONUP or WM_MOUSELEAVE or WM_NCMOUSELEAVE)
        {
            lastNativeHitLogKey = hitKey;
            return true;
        }

        if (!string.Equals(lastNativeHitLogKey, hitKey, StringComparison.Ordinal))
        {
            lastNativeHitLogKey = hitKey;
            lastNativeMouseLogTicks = Environment.TickCount64;
            return true;
        }

        var nowTicks = Environment.TickCount64;
        if (nowTicks - lastNativeMouseLogTicks < NativeMouseLogThrottleMilliseconds)
        {
            return false;
        }

        lastNativeMouseLogTicks = nowTicks;
        return true;
    }

    private static bool ShouldLogNativeWindowMessage(int msg)
    {
        return msg is WM_NCHITTEST
            or WM_SETCURSOR
            or WM_MOUSEMOVE
            or WM_LBUTTONDOWN
            or WM_LBUTTONUP
            or WM_MOUSELEAVE
            or WM_NCMOUSEMOVE
            or WM_NCLBUTTONDOWN
            or WM_NCLBUTTONUP
            or WM_NCMOUSELEAVE;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WriteTitleBarHoverDebugLog("MinimizeButtonClick", sender);
        if (OwnerWindow is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        WriteTitleBarHoverDebugLog("MaximizeRestoreButtonClick", sender);
        if (OwnerWindow is not { } window)
        {
            return;
        }

        if (IsWorkspaceMaximized(window) || window.WindowState == WindowState.Maximized)
        {
            RestoreWorkspaceWindow(window);
        }
        else
        {
            MaximizeToWorkspace(window);
        }

        UpdateMaximizeRestoreIcon();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        WriteTitleBarHoverDebugLog("CloseButtonClick", sender);
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
        WriteTitleBarHoverDebugLog("OwnerWindowStateChanged", sender);
        UpdateMaximizeRestoreIcon();
    }

    private void OwnerWindow_Activated(object? sender, EventArgs e)
    {
        WriteTitleBarHoverDebugLog("OwnerWindowActivated", sender);
        RefreshMouseHoverState("OwnerWindowActivated");
    }

    private void OwnerWindow_Deactivated(object? sender, EventArgs e)
    {
        WriteTitleBarHoverDebugLog("OwnerWindowDeactivated", sender);
    }

    private void OwnerWindow_LocationChanged(object? sender, EventArgs e)
    {
        WriteTitleBarHoverDebugLog("OwnerWindowLocationChanged", sender);
    }

    private void OwnerWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        WriteTitleBarHoverDebugLog(
            $"OwnerWindowSizeChanged width={e.NewSize.Width:0.##} height={e.NewSize.Height:0.##}",
            sender);
    }

    private void TitleBarButton_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (ShouldLogWpfMouseMoveNow())
        {
            WriteTitleBarHoverDebugLog("TitleBarButtonPreviewMouseMove", sender);
        }
    }

    private void TitleBarButton_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (ShouldLogWpfMouseMoveNow())
        {
            WriteTitleBarHoverDebugLog("TitleBarButtonMouseMove", sender);
        }
    }

    private bool ShouldLogWpfMouseMoveNow()
    {
        var nowTicks = Environment.TickCount64;
        if (nowTicks - lastWpfMouseMoveLogTicks < WpfMouseMoveLogThrottleMilliseconds)
        {
            return false;
        }

        lastWpfMouseMoveLogTicks = nowTicks;
        return true;
    }

    private void TitleBarButton_MouseEnter(object sender, WpfMouseEventArgs e)
    {
        WriteTitleBarHoverDebugLog("TitleBarButtonMouseEnter", sender);
        Dispatcher.BeginInvoke(
            () => WriteTitleBarHoverDebugLog("TitleBarButtonMouseEnterIdleSnapshot", sender),
            System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void TitleBarButton_MouseLeave(object sender, WpfMouseEventArgs e)
    {
        WriteTitleBarHoverDebugLog("TitleBarButtonMouseLeave", sender);
    }

    private void TitleBarButton_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        WriteTitleBarHoverDebugLog("TitleBarButtonGotKeyboardFocus", sender);
    }

    private void TitleBarButton_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        WriteTitleBarHoverDebugLog("TitleBarButtonLostKeyboardFocus", sender);
    }

    private void MaximizeToWorkspace(Window window)
    {
        if (isApplyingWorkspaceBounds)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        normalWindowBounds = CaptureNormalBounds(window);
        var workArea = ConvertScreenBoundsToDip(window, GetCurrentScreenWorkingArea(window));

        isApplyingWorkspaceBounds = true;
        try
        {
            if (window.WindowState == WindowState.Maximized)
            {
                window.WindowState = WindowState.Normal;
            }

            ApplyWorkspaceMaximized(window);
        }
        finally
        {
            isApplyingWorkspaceBounds = false;
        }
    }

    private void RestoreWorkspaceWindow(Window window)
    {
        if (isApplyingWorkspaceBounds)
        {
            return;
        }

        var workArea = ConvertScreenBoundsToDip(window, GetCurrentScreenWorkingArea(window));
        var restoreBounds = normalWindowBounds ?? window.RestoreBounds;
        var normalizedBounds = ModernTitleBarWorkspaceBoundsService.NormalizeRestoreBounds(
            restoreBounds,
            workArea,
            window.MinWidth,
            window.MinHeight);

        isApplyingWorkspaceBounds = true;
        try
        {
            if (window.WindowState != WindowState.Normal)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Left = normalizedBounds.Left;
            window.Top = normalizedBounds.Top;
            window.Width = normalizedBounds.Width;
            window.Height = normalizedBounds.Height;
            SetIsWorkspaceMaximized(window, false);
        }
        finally
        {
            isApplyingWorkspaceBounds = false;
        }
    }

    private static Rect CaptureNormalBounds(Window window)
    {
        if (window.WindowState != WindowState.Normal)
        {
            return window.RestoreBounds;
        }

        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        if (!double.IsFinite(width) || width <= 0 || !double.IsFinite(height) || height <= 0)
        {
            return window.RestoreBounds;
        }

        return new Rect(window.Left, window.Top, width, height);
    }

    private static DrawingRectangle GetCurrentScreenWorkingArea(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        return handle == IntPtr.Zero
            ? WinForms.Screen.PrimaryScreen?.WorkingArea ?? WinForms.Screen.AllScreens[0].WorkingArea
            : WinForms.Screen.FromHandle(handle).WorkingArea;
    }

    private static Rect ConvertScreenBoundsToDip(Window window, DrawingRectangle bounds)
    {
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is null)
        {
            return new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        }

        var transform = source.CompositionTarget.TransformFromDevice;
        var topLeft = transform.Transform(new System.Windows.Point(bounds.Left, bounds.Top));
        var bottomRight = transform.Transform(new System.Windows.Point(bounds.Right, bounds.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private void UpdateMaximizeRestoreIcon()
    {
        if (OwnerWindow is not { } window)
        {
            return;
        }

        var isMaximized = IsWorkspaceMaximized(window) || window.WindowState == WindowState.Maximized;
        MaximizeIcon.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;
    }

    public void RefreshMouseHoverState(string reason)
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                try
                {
                    Mouse.Synchronize();
                    MinimizeButton.InvalidateVisual();
                    MaximizeRestoreButton.InvalidateVisual();
                    CloseButton.InvalidateVisual();
                    WriteTitleBarHoverDebugLog($"RefreshMouseHoverState reason={reason}", this);
                }
                catch
                {
                    // Hover refresh is a best-effort input synchronization aid.
                }
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void WriteTitleBarHoverDebugLog(string stage, object? source)
    {
        if (!EnableTitleBarHoverDebugLogging)
        {
            return;
        }

        try
        {
            RollingLogFileWriter.Append(TitleBarHoverDebugLogPath, BuildTitleBarHoverSnapshot(stage, source));
        }
        catch
        {
            // Debug logging must never affect title bar interaction.
        }
    }

    private string BuildTitleBarHoverSnapshot(string stage, object? source)
    {
        var owner = OwnerWindow;
        var ownerHandle = owner is null ? IntPtr.Zero : new WindowInteropHelper(owner).Handle;
        var foregroundHandle = GetForegroundWindow();
        var screenMouse = WinForms.Control.MousePosition;
        var titleBarMouse = TryGetMousePosition(this);
        var closeMouse = TryGetMousePosition(CloseButton);
        var closeBounds = GetElementBoundsInTitleBar(CloseButton);
        var sourceElement = source as FrameworkElement;

        return $"""
[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {stage}
Owner={FormatWindow(owner)} OwnerHandle=0x{ownerHandle.ToInt64():X} ForegroundHandle=0x{foregroundHandle.ToInt64():X} IsForegroundOwner={ownerHandle != IntPtr.Zero && ownerHandle == foregroundHandle}
Source={FormatElement(sourceElement)} MouseDirectlyOver={FormatObjectType(Mouse.DirectlyOver)} KeyboardFocused={FormatObjectType(Keyboard.FocusedElement)}
ScreenMouse={screenMouse.X},{screenMouse.Y} TitleBarMouse={FormatPoint(titleBarMouse)} CloseMouse={FormatPoint(closeMouse)} CloseBoundsInTitleBar={FormatRect(closeBounds)} CloseContainsMouse={IsPointInside(closeBounds, titleBarMouse)}
TitleBar IsVisible={IsVisible} IsEnabled={IsEnabled} IsMouseOver={IsMouseOver} IsKeyboardFocusWithin={IsKeyboardFocusWithin} Actual={ActualWidth:0.##}x{ActualHeight:0.##}
Minimize {FormatButtonState(MinimizeButton)}
MaximizeRestore {FormatButtonState(MaximizeRestoreButton)}
Close {FormatButtonState(CloseButton)}

""";
    }

    private static string FormatWindow(Window? window)
    {
        if (window is null)
        {
            return "<null>";
        }

        return $"{window.GetType().Name} Title=\"{window.Title}\" IsActive={window.IsActive} IsVisible={window.IsVisible} WindowState={window.WindowState} ResizeMode={window.ResizeMode} Left={window.Left:0.##} Top={window.Top:0.##} Actual={window.ActualWidth:0.##}x{window.ActualHeight:0.##}";
    }

    private string FormatButtonState(WpfButton button)
    {
        return $"Name={button.Name} IsVisible={button.IsVisible} Visibility={button.Visibility} IsEnabled={button.IsEnabled} IsHitTestVisible={button.IsHitTestVisible} ChromeHitTest={WindowChrome.GetIsHitTestVisibleInChrome(button)} IsMouseOver={button.IsMouseOver} IsPressed={button.IsPressed} IsKeyboardFocusWithin={button.IsKeyboardFocusWithin} Actual={button.ActualWidth:0.##}x{button.ActualHeight:0.##}";
    }

    private string FormatElement(FrameworkElement? element)
    {
        if (element is null)
        {
            return FormatObjectType(element);
        }

        var name = string.IsNullOrWhiteSpace(element.Name) ? "<no-name>" : element.Name;
        return $"{element.GetType().Name} Name={name} IsVisible={element.IsVisible} IsEnabled={element.IsEnabled} IsHitTestVisible={element.IsHitTestVisible} IsMouseOver={element.IsMouseOver}";
    }

    private static string FormatObjectType(object? value)
    {
        return value is null ? "<null>" : value.GetType().FullName ?? value.GetType().Name;
    }

    private static string FormatWindowMessage(int message)
    {
        return message switch
        {
            WM_NCHITTEST => "WM_NCHITTEST",
            WM_SETCURSOR => "WM_SETCURSOR",
            WM_MOUSEMOVE => "WM_MOUSEMOVE",
            WM_LBUTTONDOWN => "WM_LBUTTONDOWN",
            WM_LBUTTONUP => "WM_LBUTTONUP",
            WM_MOUSELEAVE => "WM_MOUSELEAVE",
            WM_NCMOUSEMOVE => "WM_NCMOUSEMOVE",
            WM_NCLBUTTONDOWN => "WM_NCLBUTTONDOWN",
            WM_NCLBUTTONUP => "WM_NCLBUTTONUP",
            WM_NCMOUSELEAVE => "WM_NCMOUSELEAVE",
            _ => $"0x{message:X}"
        };
    }

    private NativeMouseSnapshot? BuildNativeMouseSnapshot(IntPtr hwnd, int message, IntPtr lParam)
    {
        if (!TryGetNativeScreenPoint(hwnd, message, lParam, out var screenPoint, out var coordinateKind))
        {
            return new NativeMouseSnapshot(
                $"NativeCoordinateKind={coordinateKind} NativeScreen=<unavailable> NativeTitleBar=<unavailable> NativeHit=<unavailable>",
                $"{coordinateKind}|<unavailable>");
        }

        var titleBarPoint = TryPointFromScreen(this, screenPoint);
        var minimizeBounds = GetElementBoundsInTitleBar(MinimizeButton);
        var maximizeBounds = GetElementBoundsInTitleBar(MaximizeRestoreButton);
        var closeBounds = GetElementBoundsInTitleBar(CloseButton);
        var nativeHit = ResolveNativeHit(titleBarPoint, minimizeBounds, maximizeBounds, closeBounds);
        var nativeCloseContains = IsPointInside(closeBounds, titleBarPoint);
        var nativeMaximizeContains = IsPointInside(maximizeBounds, titleBarPoint);
        var nativeMinimizeContains = IsPointInside(minimizeBounds, titleBarPoint);

        return new NativeMouseSnapshot(
            $"NativeCoordinateKind={coordinateKind} NativeScreen={FormatPoint(screenPoint)} NativeTitleBar={FormatPoint(titleBarPoint)} NativeHit={nativeHit} NativeMinimizeContains={nativeMinimizeContains} NativeMaximizeContains={nativeMaximizeContains} NativeCloseContains={nativeCloseContains}",
            $"{coordinateKind}|{nativeHit}|{nativeMinimizeContains}|{nativeMaximizeContains}|{nativeCloseContains}");
    }

    private sealed record NativeMouseSnapshot(string Text, string HitKey);

    private static bool TryGetNativeScreenPoint(
        IntPtr hwnd,
        int message,
        IntPtr lParam,
        out WpfPoint screenPoint,
        out string coordinateKind)
    {
        var rawPoint = DecodeLParamPoint(lParam);
        if (message is WM_NCHITTEST or WM_NCMOUSEMOVE or WM_NCLBUTTONDOWN or WM_NCLBUTTONUP)
        {
            screenPoint = new WpfPoint(rawPoint.X, rawPoint.Y);
            coordinateKind = "screen";
            return true;
        }

        if (message is WM_MOUSEMOVE or WM_LBUTTONDOWN or WM_LBUTTONUP)
        {
            var clientPoint = new NativePoint
            {
                X = rawPoint.X,
                Y = rawPoint.Y
            };
            if (ClientToScreen(hwnd, ref clientPoint))
            {
                screenPoint = new WpfPoint(clientPoint.X, clientPoint.Y);
                coordinateKind = "client-to-screen";
                return true;
            }

            screenPoint = default;
            coordinateKind = "client-to-screen-failed";
            return false;
        }

        screenPoint = default;
        coordinateKind = "no-coordinate";
        return false;
    }

    private static NativePoint DecodeLParamPoint(IntPtr lParam)
    {
        var value = unchecked((int)lParam.ToInt64());
        return new NativePoint
        {
            X = unchecked((short)(value & 0xFFFF)),
            Y = unchecked((short)((value >> 16) & 0xFFFF))
        };
    }

    private static WpfPoint? TryPointFromScreen(Visual visual, WpfPoint screenPoint)
    {
        try
        {
            return visual.PointFromScreen(screenPoint);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveNativeHit(
        WpfPoint? titleBarPoint,
        Rect? minimizeBounds,
        Rect? maximizeBounds,
        Rect? closeBounds)
    {
        if (titleBarPoint is not { } point)
        {
            return "<unavailable>";
        }

        if (IsPointInside(closeBounds, point))
        {
            return "CloseButton";
        }

        if (IsPointInside(maximizeBounds, point))
        {
            return "MaximizeRestoreButton";
        }

        if (IsPointInside(minimizeBounds, point))
        {
            return "MinimizeButton";
        }

        return point.Y >= 0 && point.Y <= 44
            ? "TitleBarNonButton"
            : "OutsideTitleBar";
    }

    private static WpfPoint? TryGetMousePosition(IInputElement element)
    {
        try
        {
            return Mouse.GetPosition(element);
        }
        catch
        {
            return null;
        }
    }

    private Rect? GetElementBoundsInTitleBar(FrameworkElement element)
    {
        try
        {
            if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                return null;
            }

            var transform = element.TransformToAncestor(this);
            return transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        }
        catch
        {
            return null;
        }
    }

    private static string FormatPoint(WpfPoint? point)
    {
        return point is { } value ? $"{value.X:0.##},{value.Y:0.##}" : "<unavailable>";
    }

    private static string FormatRect(Rect? rect)
    {
        return rect is { } value
            ? $"L={value.Left:0.##} T={value.Top:0.##} W={value.Width:0.##} H={value.Height:0.##}"
            : "<unavailable>";
    }

    private static bool IsPointInside(Rect? rect, WpfPoint? point)
    {
        return rect is { } bounds && point is { } value && bounds.Contains(value);
    }
}
