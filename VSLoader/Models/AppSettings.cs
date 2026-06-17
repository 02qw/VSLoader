namespace VSLoader.Models;

public sealed class AppSettings
{
    public string VSCodePath { get; set; } = string.Empty;

    public string SoftwareUpdateManifestPath { get; set; } = string.Empty;

    public string LastWorkspaceId { get; set; } = string.Empty;

    public bool OpenLastWorkspaceOnStartup { get; set; } = true;

    public bool MigrationCompleted { get; set; }

    public List<WorkspaceInfo> Workspaces { get; set; } = new();
}
