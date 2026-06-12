namespace VSLoader.Models;

public sealed class FactoryMapLoadResult
{
    public FactoryMapLoadResult(FactoryMapConfig config, bool success, string? errorMessage)
    {
        Config = config;
        Success = success;
        ErrorMessage = errorMessage;
    }

    public FactoryMapConfig Config { get; }

    public bool Success { get; }

    public string? ErrorMessage { get; }
}
