namespace VSLoader.Services;

public sealed class AdminUiDownloadResult
{
    public int SuccessCount { get; init; }

    public int FailedCount { get; init; }

    public List<string> Messages { get; init; } = new();
}
