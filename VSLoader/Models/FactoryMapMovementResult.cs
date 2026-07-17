namespace VSLoader.Models;

public sealed class FactoryMapMovementResult
{
    private FactoryMapMovementResult(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public static FactoryMapMovementResult Succeeded() => new(true, null);

    public static FactoryMapMovementResult Failed(string errorMessage) => new(false, errorMessage);
}
