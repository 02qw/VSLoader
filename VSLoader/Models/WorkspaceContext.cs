using System.IO;

namespace VSLoader.Models;

public sealed class WorkspaceContext
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string RootPath { get; init; } = string.Empty;

    public string MetadataPath => Path.Combine(RootPath, "workspace.json");

    public string ConfigPath => Path.Combine(RootPath, "config.json");

    public string WindowLayoutPath => Path.Combine(RootPath, "window-layout.json");

    public string FactoryMapLayoutPath => Path.Combine(RootPath, "factory-map.layout.json");

    public string UiDownloadDirectory => Path.Combine(RootPath, "UIdownload");

    public string UpdateTimePath => Path.Combine(RootPath, "updateTime.json");
}
