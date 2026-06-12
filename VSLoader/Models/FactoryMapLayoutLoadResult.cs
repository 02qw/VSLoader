namespace VSLoader.Models;

public sealed class FactoryMapLayoutLoadResult
{
    public FactoryMapLayoutLoadResult(
        FactoryMapDeviceViewData map,
        bool success,
        string? errorMessage,
        int appliedDeviceCount = 0,
        int skippedDeviceCount = 0,
        int keptEdgeCount = 0,
        int skippedEdgeCount = 0)
    {
        Map = map;
        Success = success;
        ErrorMessage = errorMessage;
        AppliedDeviceCount = appliedDeviceCount;
        SkippedDeviceCount = skippedDeviceCount;
        KeptEdgeCount = keptEdgeCount;
        SkippedEdgeCount = skippedEdgeCount;
    }

    public FactoryMapDeviceViewData Map { get; }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public int AppliedDeviceCount { get; }

    public int SkippedDeviceCount { get; }

    public int KeptEdgeCount { get; }

    public int SkippedEdgeCount { get; }
}
