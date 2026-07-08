using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace VSLoader.Services;

public interface ICriticalInputOverlayScope : IDisposable
{
    bool IsActive { get; }
}

public sealed class CriticalInputOverlayService
{
    internal const int ExtendedWindowStyleIndex = -20;
    internal const int NoActivateExtendedWindowStyle = 0x08000000;

    internal static ICriticalInputOverlayScope ShowInactiveScope()
    {
        return CriticalInputOverlayScope.Inactive;
    }

    public ICriticalInputOverlayScope Show()
    {
        try
        {
            var windows = new List<Window>();
            foreach (var screen in WinForms.Screen.AllScreens)
            {
                var bounds = screen.Bounds;
                var window = new Window
                {
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false,
                    Topmost = true,
                    AllowsTransparency = true,
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0)),
                    Left = bounds.Left,
                    Top = bounds.Top,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    Focusable = false,
                    IsHitTestVisible = true,
                    ShowActivated = false
                };
                window.SourceInitialized += (_, _) =>
                {
                    var handle = new WindowInteropHelper(window).Handle;
                    ApplyNoActivateExtendedStyle(handle);
                };
                window.Show();
                windows.Add(window);
            }

            return new CriticalInputOverlayScope(windows);
        }
        catch
        {
            return CriticalInputOverlayScope.Inactive;
        }
    }

    internal static int ApplyNoActivateExtendedStyle(
        IntPtr handle,
        Func<IntPtr, int, int> getWindowLong,
        Func<IntPtr, int, int, int> setWindowLong)
    {
        if (handle == IntPtr.Zero)
        {
            return 0;
        }

        var currentStyle = getWindowLong(handle, ExtendedWindowStyleIndex);
        var updatedStyle = currentStyle | NoActivateExtendedWindowStyle;
        if (updatedStyle != currentStyle)
        {
            setWindowLong(handle, ExtendedWindowStyleIndex, updatedStyle);
        }

        return updatedStyle;
    }

    private static int ApplyNoActivateExtendedStyle(IntPtr handle)
    {
        return ApplyNoActivateExtendedStyle(handle, GetWindowLong, SetWindowLong);
    }

    private sealed class CriticalInputOverlayScope : ICriticalInputOverlayScope
    {
        public static readonly ICriticalInputOverlayScope Inactive = new CriticalInputOverlayScope([]);

        private readonly IReadOnlyList<Window> windows;
        private bool disposed;

        public CriticalInputOverlayScope(IReadOnlyList<Window> windows)
        {
            this.windows = windows;
            IsActive = windows.Count > 0;
        }

        public bool IsActive { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (var window in windows)
            {
                try
                {
                    window.Close();
                }
                catch
                {
                    // Overlay cleanup must not break the AdminUI automation flow.
                }
            }
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
