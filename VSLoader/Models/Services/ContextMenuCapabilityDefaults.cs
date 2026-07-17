using VSLoader.Models;

namespace VSLoader.Services;

public static class ContextMenuCapabilityDefaults
{
    public static ContextMenuCapabilityCollectionConfig Create()
    {
        var items = ContextMenuBuiltInActionIds.All
            .Select((actionId, index) => CreateBuiltIn(actionId, index * 10))
            .ToList();
        return new ContextMenuCapabilityCollectionConfig { SchemaVersion = 1, Items = items };
    }

    public static ContextMenuCapabilityDefinition CreateBuiltIn(string actionId, int order)
    {
        return new ContextMenuCapabilityDefinition
        {
            Id = actionId,
            Name = ContextMenuBuiltInActionIds.GetDisplayName(actionId),
            Kind = ContextMenuCapabilityKinds.BuiltIn,
            BuiltInActionId = actionId,
            Enabled = true,
            Order = order,
            ShowInShortcutList = true,
            ShowInFactoryMap = true,
            RequiresExistingTargetPath = true
        };
    }
}
