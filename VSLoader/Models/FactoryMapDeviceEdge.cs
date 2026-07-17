namespace VSLoader.Models;

public sealed class FactoryMapDeviceEdge
{
    public string From { get; set; } = string.Empty;

    public string FromKind { get; set; } = FactoryMapEndpointKinds.Device;

    public string FromPort { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    public string ToKind { get; set; } = FactoryMapEndpointKinds.Device;

    public string ToPort { get; set; } = string.Empty;

    public List<FactoryMapPoint> Points { get; set; } = [];
}
