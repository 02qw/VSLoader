namespace VSLoader.Models;

public sealed class FactoryMapLineArrangementResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public int ArrangedRouteCount { get; init; }

    public int RemovedBendCount { get; init; }

    public int CreatedBendCount { get; init; }

    public static FactoryMapLineArrangementResult Succeeded(
        int arrangedRouteCount,
        int removedBendCount,
        int createdBendCount)
    {
        return new FactoryMapLineArrangementResult
        {
            Success = true,
            ArrangedRouteCount = arrangedRouteCount,
            RemovedBendCount = removedBendCount,
            CreatedBendCount = createdBendCount
        };
    }

    public static FactoryMapLineArrangementResult Failed(string errorMessage)
    {
        return new FactoryMapLineArrangementResult { ErrorMessage = errorMessage };
    }
}
