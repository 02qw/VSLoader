namespace VSLoader.Models;

public sealed class FactoryMapLineArrangementResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public static FactoryMapLineArrangementResult Succeeded()
    {
        return new FactoryMapLineArrangementResult { Success = true };
    }

    public static FactoryMapLineArrangementResult Failed(string errorMessage)
    {
        return new FactoryMapLineArrangementResult { ErrorMessage = errorMessage };
    }
}
