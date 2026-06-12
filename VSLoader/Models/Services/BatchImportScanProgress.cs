namespace VSLoader.Services;

public sealed class BatchImportScanProgress
{
    public int CompletedCount { get; init; }

    public int TotalCount { get; init; }

    public string CurrentFolderName { get; init; } = string.Empty;

    public string Stage { get; init; } = string.Empty;
}
