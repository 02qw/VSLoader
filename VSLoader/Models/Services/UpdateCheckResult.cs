namespace VSLoader.Services;

public sealed class UpdateCheckResult
{
    public List<string> UpdatedItems { get; } = new();

    public List<string> Failures { get; } = new();

    public DateTime? DetectedRulesWriteTimeUtc { get; set; }

    public DateTime? DetectedMapWriteTimeUtc { get; set; }

    public string DetectedSoftwareVersion { get; set; } = string.Empty;
}
