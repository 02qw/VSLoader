namespace VSLoader.Models;

public sealed class GlobalConfigPackage
{
    public int SchemaVersion { get; set; } = 1;

    public string AppName { get; set; } = "VSLoader";

    public string ExportedAt { get; set; } = string.Empty;

    public GlobalProgramSettings ProgramSettings { get; set; } = new();

    public AppConfig WorkspaceConfig { get; set; } = new();

    public FactoryMapLayoutConfig? FactoryMapLayout { get; set; }
}
