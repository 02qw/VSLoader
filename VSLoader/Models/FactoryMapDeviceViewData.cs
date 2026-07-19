namespace VSLoader.Models;

public sealed class FactoryMapDeviceViewData
{
    public bool TopologyAuthoritative { get; set; }

    public FactoryMapCanvas Canvas { get; set; } = new() { Width = 1600, Height = 900 };

    public List<FactoryMapDeviceViewNode> Devices { get; set; } = [];

    public List<FactoryMapConnectionPoint> ConnectionPoints { get; set; } = [];

    public List<FactoryMapSegment> Segments { get; set; } = [];

    public List<FactoryMapConnectorViewNode> Connectors { get; set; } = [];

    public List<FactoryMapDeviceEdgeViewData> Edges { get; set; } = [];

    public int InvalidEdgeCount { get; set; }

    public int InvalidSegmentCount { get; set; }

    public bool RequiresPersistence { get; set; }

    public List<string> LoadWarnings { get; set; } = [];
}
