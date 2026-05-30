namespace VSLoader.Services;

public sealed class AdminUiDownloadProgress
{
    public int TotalCount { get; init; }

    public int CompletedCount { get; init; }

    public int SuccessCount { get; init; }

    public int FailedCount { get; init; }

    public string CurrentShortcutName { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
