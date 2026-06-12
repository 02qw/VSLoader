namespace VSLoader.Models;

public sealed class FactoryMapDeviceEdgeViewData
{
    public FactoryMapDeviceViewNode From { get; set; } = new();

    public FactoryMapDeviceViewNode To { get; set; } = new();
}
