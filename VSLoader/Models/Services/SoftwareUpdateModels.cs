namespace VSLoader.Services;

public sealed class SoftwareUpdateRequest
{
    public string ManifestPath { get; init; } = string.Empty;

    public Version CurrentVersion { get; init; } = new(0, 0);

    public string TargetDirectory { get; init; } = string.Empty;

    public string UpdatesRoot { get; init; } = string.Empty;

    public int CurrentProcessId { get; init; }

    public string MainExeName { get; init; } = "VSLoader.exe";

    public string UpdaterExeName { get; init; } = "VSLoader.Updater.exe";
}

public sealed class SoftwareUpdateResult
{
    public bool Success { get; init; }

    public bool UpdateAvailable { get; init; }

    public string Message { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public string? StagingDirectory { get; init; }

    public string? UpdaterPath { get; init; }

    public string UpdaterArguments { get; init; } = string.Empty;

    public static SoftwareUpdateResult OkNoUpdate(string message)
    {
        return new SoftwareUpdateResult { Success = true, UpdateAvailable = false, Message = message };
    }

    public static SoftwareUpdateResult OkUpdate(string message, string stagingDirectory, string updaterPath, string updaterArguments)
    {
        return new SoftwareUpdateResult
        {
            Success = true,
            UpdateAvailable = true,
            Message = message,
            StagingDirectory = stagingDirectory,
            UpdaterPath = updaterPath,
            UpdaterArguments = updaterArguments
        };
    }

    public static SoftwareUpdateResult Fail(string errorMessage)
    {
        return new SoftwareUpdateResult { Success = false, ErrorMessage = errorMessage };
    }
}

public sealed class SoftwareUpdateAvailabilityResult
{
    public bool Success { get; init; }

    public bool UpdateAvailable { get; init; }

    public string Message { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public string ReleaseNotes { get; init; } = string.Empty;

    public static SoftwareUpdateAvailabilityResult OkNoUpdate(string message, string releaseNotes)
    {
        return new SoftwareUpdateAvailabilityResult
        {
            Success = true,
            UpdateAvailable = false,
            Message = message,
            ReleaseNotes = releaseNotes
        };
    }

    public static SoftwareUpdateAvailabilityResult OkUpdate(string message, string releaseNotes)
    {
        return new SoftwareUpdateAvailabilityResult
        {
            Success = true,
            UpdateAvailable = true,
            Message = message,
            ReleaseNotes = releaseNotes
        };
    }

    public static SoftwareUpdateAvailabilityResult Fail(string errorMessage)
    {
        return new SoftwareUpdateAvailabilityResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

public sealed class SoftwareUpdateProgress
{
    public int ProgressValue { get; init; }

    public int ProgressMaximum { get; init; } = 100;

    public string StepText { get; init; } = string.Empty;

    public string CurrentItemText { get; init; } = string.Empty;
}
