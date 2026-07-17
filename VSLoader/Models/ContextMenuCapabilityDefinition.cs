namespace VSLoader.Models;

public sealed class ContextMenuCapabilityDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string BuiltInActionId { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public int Order { get; set; }

    public bool ShowInShortcutList { get; set; } = true;

    public bool ShowInFactoryMap { get; set; } = true;

    public bool ConfirmBeforeExecute { get; set; }

    public bool RequiresExistingTargetPath { get; set; } = true;

    public PowerShellCapabilityConfig PowerShell { get; set; } = new();

    public WebCapabilityConfig Web { get; set; } = new();

    public ContextMenuCapabilityDefinition Clone()
    {
        return new ContextMenuCapabilityDefinition
        {
            Id = Id,
            Name = Name,
            Kind = Kind,
            BuiltInActionId = BuiltInActionId,
            Enabled = Enabled,
            Order = Order,
            ShowInShortcutList = ShowInShortcutList,
            ShowInFactoryMap = ShowInFactoryMap,
            ConfirmBeforeExecute = ConfirmBeforeExecute,
            RequiresExistingTargetPath = RequiresExistingTargetPath,
            PowerShell = PowerShell?.Clone() ?? new PowerShellCapabilityConfig(),
            Web = Web?.Clone() ?? new WebCapabilityConfig()
        };
    }
}
