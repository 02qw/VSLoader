namespace VSLoader.Updater.Services;

public sealed class UpdaterOptions
{
    public string Mode { get; init; } = "apply";

    public int ProcessId { get; init; }

    public string TargetDirectory { get; init; } = string.Empty;

    public string StagingDirectory { get; init; } = string.Empty;

    public string MainExeName { get; init; } = "VSLoader.exe";

    public string UpdatesRoot { get; init; } = string.Empty;

    public string ManifestPath { get; init; } = string.Empty;

    public Version CurrentVersion { get; init; } = new(0, 0);
}

public sealed class UpdaterArgumentParseResult
{
    private UpdaterArgumentParseResult(bool success, UpdaterOptions? options, string errorMessage)
    {
        Success = success;
        Options = options;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public UpdaterOptions? Options { get; }

    public string ErrorMessage { get; }

    public static UpdaterArgumentParseResult Ok(UpdaterOptions options)
    {
        return new UpdaterArgumentParseResult(true, options, string.Empty);
    }

    public static UpdaterArgumentParseResult Fail(string errorMessage)
    {
        return new UpdaterArgumentParseResult(false, null, errorMessage);
    }
}
