namespace VSLoader.Models;

public sealed class FactoryMapDeviceViewData
{
    public FactoryMapCanvas Canvas { get; set; } = new() { Width = 1600, Height = 900 };

    public List<FactoryMapDeviceViewNode> Devices { get; set; } = [];

    public List<FactoryMapDeviceEdgeViewData> Edges { get; set; } = [];

    public int InvalidEdgeCount { get; set; }
}
