namespace VSLoader.Models;

public sealed class FactoryMapRouteResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<FactoryMapPoint> Points { get; init; } = [];

    public static FactoryMapRouteResult Succeeded(IEnumerable<FactoryMapPoint> points)
    {
        return new FactoryMapRouteResult
        {
            Success = true,
            Points = points.Select(point => new FactoryMapPoint { X = point.X, Y = point.Y }).ToArray()
        };
    }

    public static FactoryMapRouteResult Failed(string errorMessage)
    {
        return new FactoryMapRouteResult { ErrorMessage = errorMessage };
    }
}
