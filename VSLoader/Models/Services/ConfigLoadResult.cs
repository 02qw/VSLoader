using VSLoader.Models;

namespace VSLoader.Services;

public sealed class ConfigLoadResult
{
    public ConfigLoadResult(AppConfig config, bool success, string? errorMessage, bool hasInvalidConfigFile = false)
    {
        Config = config;
        Success = success;
        ErrorMessage = errorMessage;
        HasInvalidConfigFile = hasInvalidConfigFile;
    }

    public AppConfig Config { get; }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public bool HasInvalidConfigFile { get; }
}
