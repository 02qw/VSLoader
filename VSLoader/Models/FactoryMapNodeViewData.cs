namespace VSLoader.Models;

public sealed class FactoryMapNodeViewData
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public List<FactoryMapMachineNode> Machines { get; set; } = [];
}
