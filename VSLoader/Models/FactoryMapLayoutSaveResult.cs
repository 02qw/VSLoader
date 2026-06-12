namespace VSLoader.Models;

public sealed class FactoryMapLayoutSaveResult
{
    public FactoryMapLayoutSaveResult(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public string? ErrorMessage { get; }
}
