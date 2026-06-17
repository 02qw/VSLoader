namespace VSLoader.Updater.Services;

public sealed class UpdaterProgress
{
    public int Value { get; init; }

    public string Message { get; init; } = string.Empty;

    public string ReleaseNotes { get; init; } = string.Empty;
}

public sealed class UpdaterUpdateResult
{
    private UpdaterUpdateResult(bool success, bool restartMainApp, string message, string errorMessage, string? errorLogPath, string releaseNotes)
    {
        Success = success;
        RestartMainApp = restartMainApp;
        Message = message;
        ErrorMessage = errorMessage;
        ErrorLogPath = errorLogPath;
        ReleaseNotes = releaseNotes;
    }

    public bool Success { get; }

    public bool RestartMainApp { get; }

    public string Message { get; }

    public string ErrorMessage { get; }

    public string? ErrorLogPath { get; }

    public string ReleaseNotes { get; }

    public static UpdaterUpdateResult Ok(string message, bool restartMainApp, string releaseNotes = "")
    {
        return new UpdaterUpdateResult(true, restartMainApp, message, string.Empty, null, releaseNotes);
    }

    public static UpdaterUpdateResult Fail(string errorMessage, string? errorLogPath = null, string releaseNotes = "")
    {
        return new UpdaterUpdateResult(false, false, string.Empty, errorMessage, errorLogPath, releaseNotes);
    }
}
