namespace VSLoader.Models;

public sealed class FactoryMapTopologyOperationResult
{
    private FactoryMapTopologyOperationResult(
        bool success,
        string? errorMessage,
        string? pointId,
        bool reusedEndpoint)
    {
        Success = success;
        ErrorMessage = errorMessage;
        PointId = pointId;
        ReusedEndpoint = reusedEndpoint;
    }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public string? PointId { get; }

    public bool ReusedEndpoint { get; }

    public static FactoryMapTopologyOperationResult Succeeded(
        string? pointId = null,
        bool reusedEndpoint = false)
    {
        return new FactoryMapTopologyOperationResult(true, null, pointId, reusedEndpoint);
    }

    public static FactoryMapTopologyOperationResult Failed(string errorMessage)
    {
        return new FactoryMapTopologyOperationResult(false, errorMessage, null, false);
    }
}
