namespace VSLoader.Models;

public sealed class ContextMenuCapabilityCollectionConfig
{
    public int SchemaVersion { get; set; } = 1;

    public List<ContextMenuCapabilityDefinition> Items { get; set; } = new();

    public ContextMenuCapabilityCollectionConfig Clone()
    {
        return new ContextMenuCapabilityCollectionConfig
        {
            SchemaVersion = SchemaVersion,
            Items = Items?.Select(item => item.Clone()).ToList() ?? []
        };
    }
}
