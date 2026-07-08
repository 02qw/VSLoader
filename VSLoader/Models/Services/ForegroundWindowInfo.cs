namespace VSLoader.Services;

public sealed class ForegroundWindowInfo
{
    public IntPtr Handle { get; init; }

    public string Title { get; init; } = string.Empty;

    public string ProcessName { get; init; } = string.Empty;

    public string ClassName { get; init; } = string.Empty;
}
