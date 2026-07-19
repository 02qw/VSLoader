namespace VSLoader.Models;

public sealed class UpdateTimeState
{
    public Dictionary<string, UpdateGlobalConfigState> GlobalConfigs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Retained for compatibility with pre-path-scoped updateTime.json files.
    public UpdateGlobalConfigState GlobalConfig { get; set; } = new();

    public UpdateFileState Rules { get; set; } = new();

    public UpdateFileState Map { get; set; } = new();

    public UpdateSoftwareState Software { get; set; } = new();
}

public sealed record LegacyUpdateTimeSource(
    string UpdateTimePath,
    string GlobalConfigPackagePath);

public sealed class UpdateFileState
{
    public DateTime? LastUsedWriteTimeUtc { get; set; }
}

public sealed class UpdateGlobalConfigState
{
    public DateTime? LastSeenWriteTimeUtc { get; set; }

    public DateTimeOffset? LastUsedExportedAt { get; set; }
}

public sealed class UpdateSoftwareState
{
    public string LastUsedVersion { get; set; } = string.Empty;
}
