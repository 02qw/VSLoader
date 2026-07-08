using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace VSLoader.Services;

public sealed class TopLevelWindowService
{
    private const int MaxWindowTitleLength = 512;
    private const int MaxClassNameLength = 256;

    public IReadOnlyList<ForegroundWindowInfo> GetTopLevelWindows()
    {
        var windows = new List<ForegroundWindowInfo>();
        _ = EnumWindows((handle, _) =>
        {
            var window = TryCreateWindowInfo(handle);
            if (window is not null)
            {
                windows.Add(window);
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static ForegroundWindowInfo? TryCreateWindowInfo(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !IsWindowVisible(handle) || IsIconic(handle))
        {
            return null;
        }

        try
        {
            var titleBuilder = new StringBuilder(MaxWindowTitleLength);
            _ = GetWindowText(handle, titleBuilder, titleBuilder.Capacity);
            var classNameBuilder = new StringBuilder(MaxClassNameLength);
            _ = GetClassName(handle, classNameBuilder, classNameBuilder.Capacity);
            var title = titleBuilder.ToString();
            var className = classNameBuilder.ToString();
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(className))
            {
                return null;
            }

            _ = GetWindowThreadProcessId(handle, out var processId);
            return new ForegroundWindowInfo
            {
                Handle = handle,
                Title = title,
                ProcessName = GetProcessName(processId),
                ClassName = className
            };
        }
        catch
        {
            return null;
        }
    }

    private static string GetProcessName(uint processId)
    {
        if (processId == 0)
        {
            return string.Empty;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
