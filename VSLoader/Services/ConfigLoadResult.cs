using VSLoader.Models;

namespace VSLoader.Services;

public sealed class ConfigLoadResult
{
    public ConfigLoadResult(AppConfig config, bool success, string? errorMessage)
    {
        Config = config;
        Success = success;
        ErrorMessage = errorMessage;
    }

    public AppConfig Config { get; }

    public bool Success { get; }

    public string? ErrorMessage { get; }
}
