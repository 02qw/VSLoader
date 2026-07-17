using System.Text.Json.Serialization;

namespace VSLoader.Models;

public sealed class GlobalConfigPackage
{
    public int SchemaVersion { get; set; } = 1;

    public string AppName { get; set; } = "VSLoader";

    public string ExportedAt { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GlobalConfigWorkspaceSection? Workspace { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GlobalProgramSettings? MachineSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GlobalProgramSettings? ProgramSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AppConfig? WorkspaceConfig { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FactoryMapLayoutConfig? FactoryMapLayout { get; set; }
}
