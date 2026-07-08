using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace VSLoader.Services;

public sealed class ForegroundWindowService
{
    private const int MaxWindowTitleLength = 512;
    private const int MaxClassNameLength = 256;

    public ForegroundWindowInfo? GetForegroundWindowInfo()
    {
        try
        {
            var handle = GetForegroundWindow();
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var titleBuilder = new StringBuilder(MaxWindowTitleLength);
            _ = GetWindowText(handle, titleBuilder, titleBuilder.Capacity);
            var classNameBuilder = new StringBuilder(MaxClassNameLength);
            _ = GetClassName(handle, classNameBuilder, classNameBuilder.Capacity);
            _ = GetWindowThreadProcessId(handle, out var processId);

            return new ForegroundWindowInfo
            {
                Handle = handle,
                Title = titleBuilder.ToString(),
                ProcessName = GetProcessName(processId),
                ClassName = classNameBuilder.ToString()
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
