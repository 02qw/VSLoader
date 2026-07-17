namespace VSLoader.Models;

public sealed class FactoryMapConnectionPoint
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = FactoryMapConnectionPointKinds.Free;

    public string OwnerNodeId { get; set; } = string.Empty;

    public string Side { get; set; } = string.Empty;

    public string JunctionAxis { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }
}
