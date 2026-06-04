namespace VSLoader.Models;

public sealed class AppConfig
{
    public string VSCodePath { get; set; } = string.Empty;

    public List<ShortcutItem> Shortcuts { get; set; } = new();

    public AdminUiConfig AdminUi { get; set; } = new();

    public HotkeyConfig Hotkey { get; set; } = new();

    public BatchImportConfig BatchImport { get; set; } = new();

    public WebUiConfig WebUi { get; set; } = new();
}
