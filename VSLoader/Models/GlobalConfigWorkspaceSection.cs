namespace VSLoader.Models;

public sealed class GlobalConfigWorkspaceSection
{
    public GlobalConfigWorkspaceSource Source { get; set; } = new();

    public GlobalConfigWorkspaceSettings? Settings { get; set; }

    public FactoryMapLayoutConfig? FactoryMapLayout { get; set; }

    public GlobalConfigInterfacePreferences? InterfacePreferences { get; set; }
}

public sealed class GlobalConfigWorkspaceSource
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

public sealed class GlobalConfigWorkspaceSettings
{
    public List<ShortcutItem> Shortcuts { get; set; } = [];

    public AdminUiConfig AdminUi { get; set; } = new();

    public HotkeyConfig Hotkey { get; set; } = new();

    public MapHotkeyConfig MapHotkey { get; set; } = new();

    public BatchImportConfig BatchImport { get; set; } = new();

    public WebUiConfig WebUi { get; set; } = new();

    public GlobalConfigWorkspaceUpdateSettings UpdateCheck { get; set; } = new();

    public ContextMenuCapabilityCollectionConfig ContextMenuCapabilities { get; set; } = new();

    public CodeCompareConfig CodeCompare { get; set; } = new();
}

public sealed class GlobalConfigWorkspaceUpdateSettings
{
    public string GlobalConfigPackagePath { get; set; } = string.Empty;
}

public sealed class GlobalConfigInterfacePreferences
{
    public List<string> SettingsPageOrder { get; set; } = [];

    public string FactoryMapWindowState { get; set; } = FactoryMapWindowStateKinds.Normal;

    public FactoryMapViewStateConfig? FactoryMapView { get; set; }

    public ShortcutGridColumnLayoutConfig? ShortcutGridColumns { get; set; }
}
