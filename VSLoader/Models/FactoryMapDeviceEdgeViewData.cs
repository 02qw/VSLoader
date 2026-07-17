namespace VSLoader.Models;

public sealed class FactoryMapDeviceEdgeViewData
{
    public FactoryMapEndpointViewData From { get; set; } = new();

    public string FromPort { get; set; } = FactoryMapPortKinds.Right;

    public FactoryMapEndpointViewData To { get; set; } = new();

    public string ToPort { get; set; } = FactoryMapPortKinds.Left;

    public List<FactoryMapPoint> Points { get; set; } = [];
}
