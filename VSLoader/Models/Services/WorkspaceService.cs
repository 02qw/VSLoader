using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class WorkspaceService
{
    private const string DefaultWorkspaceId = "default";
    private const string DefaultWorkspaceName = "默认工作区";
    private const string DefaultWorkspaceDirectoryName = "Default";
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    public WorkspaceService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VSLoader"))
    {
    }

    public WorkspaceService(string appDataDirectory)
    {
        AppDataDirectory = appDataDirectory;
    }

    public string AppDataDirectory { get; }

    public string WorkspacesDirectory => Path.Combine(AppDataDirectory, "Workspaces");

    public WorkspaceContext EnsureDefaultWorkspace(AppSettings settings)
    {
        var existing = settings.Workspaces.FirstOrDefault(workspace =>
            string.Equals(workspace.Id, DefaultWorkspaceId, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            EnsureWorkspaceFiles(existing);
            if (string.IsNullOrWhiteSpace(settings.LastWorkspaceId))
            {
                settings.LastWorkspaceId = existing.Id;
            }

            return ToContext(existing);
        }

        Directory.CreateDirectory(WorkspacesDirectory);
        var now = DateTime.Now;
        var info = new WorkspaceInfo
        {
            Id = DefaultWorkspaceId,
            Name = DefaultWorkspaceName,
            Path = Path.Combine(WorkspacesDirectory, DefaultWorkspaceDirectoryName),
            CreatedAt = now,
            UpdatedAt = now
        };

        settings.Workspaces.Add(info);
        if (string.IsNullOrWhiteSpace(settings.LastWorkspaceId))
        {
            settings.LastWorkspaceId = info.Id;
        }

        EnsureWorkspaceFiles(info);
        return ToContext(info);
    }

    public WorkspaceContext CreateWorkspace(AppSettings settings, string displayName)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? "新工作区" : displayName.Trim();
        var id = CreateNextEnglishWorkspaceId(settings.Workspaces);
        var now = DateTime.Now;
        var info = new WorkspaceInfo
        {
            Id = id,
            Name = name,
            Path = Path.Combine(WorkspacesDirectory, id),
            CreatedAt = now,
            UpdatedAt = now
        };

        settings.Workspaces.Add(info);
        EnsureWorkspaceFiles(info);
        return ToContext(info);
    }

    public WorkspaceContext ResolveStartupWorkspace(AppSettings settings)
    {
        if (settings.OpenLastWorkspaceOnStartup && !string.IsNullOrWhiteSpace(settings.LastWorkspaceId))
        {
            var lastWorkspace = settings.Workspaces.FirstOrDefault(workspace =>
                string.Equals(workspace.Id, settings.LastWorkspaceId, StringComparison.OrdinalIgnoreCase));
            if (lastWorkspace is not null && IsWorkspaceUsable(lastWorkspace))
            {
                EnsureWorkspaceFiles(lastWorkspace);
                return ToContext(lastWorkspace);
            }
        }

        var firstUsable = settings.Workspaces.FirstOrDefault(IsWorkspaceUsable);
        if (firstUsable is not null)
        {
            settings.LastWorkspaceId = firstUsable.Id;
            EnsureWorkspaceFiles(firstUsable);
            return ToContext(firstUsable);
        }

        return EnsureDefaultWorkspace(settings);
    }

    public bool IsWorkspaceUsable(WorkspaceInfo workspace)
    {
        return !string.IsNullOrWhiteSpace(workspace.Id)
            && !string.IsNullOrWhiteSpace(workspace.Path)
            && Directory.Exists(workspace.Path);
    }

    public WorkspaceContext ResolveWorkspace(WorkspaceInfo workspace)
    {
        if (!IsWorkspaceUsable(workspace))
        {
            throw new InvalidOperationException("工作区不可用或文件夹不存在。");
        }

        EnsureWorkspaceFiles(workspace);
        return ToContext(workspace);
    }

    public SaveResult RenameWorkspace(WorkspaceInfo workspace, string newName)
    {
        try
        {
            if (!IsWorkspaceUsable(workspace))
            {
                return SaveResult.Fail("工作区不可用或文件夹不存在。");
            }

            var trimmedName = newName.Trim();
            var now = DateTime.Now;
            workspace.Name = trimmedName;
            workspace.UpdatedAt = now;

            var metadataPath = Path.Combine(workspace.Path, "workspace.json");
            WorkspaceMetadata metadata = new();
            if (File.Exists(metadataPath))
            {
                var json = File.ReadAllText(metadataPath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    metadata = JsonSerializer.Deserialize<WorkspaceMetadata>(json, jsonOptions) ?? new WorkspaceMetadata();
                }
            }

            metadata.Id = workspace.Id;
            metadata.Name = trimmedName;
            if (metadata.CreatedAt == default)
            {
                metadata.CreatedAt = workspace.CreatedAt == default ? now : workspace.CreatedAt;
            }

            metadata.UpdatedAt = now;
            File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, jsonOptions));
            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
        }
    }

    public SaveResult DeleteWorkspace(WorkspaceInfo workspace)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(workspace.Path))
            {
                return SaveResult.Fail("工作区路径为空，无法删除。");
            }

            if (!Directory.Exists(workspace.Path))
            {
                return SaveResult.Fail("工作区文件夹不存在，无法删除。");
            }

            Directory.Delete(workspace.Path, recursive: true);
            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Fail($"删除工作区文件夹失败：{ex.Message}");
        }
    }

    private void EnsureWorkspaceFiles(WorkspaceInfo workspace)
    {
        Directory.CreateDirectory(workspace.Path);
        Directory.CreateDirectory(Path.Combine(workspace.Path, "UIdownload"));

        var metadataPath = Path.Combine(workspace.Path, "workspace.json");
        if (!File.Exists(metadataPath))
        {
            var metadata = new WorkspaceMetadata
            {
                Id = workspace.Id,
                Name = workspace.Name,
                CreatedAt = workspace.CreatedAt == default ? DateTime.Now : workspace.CreatedAt,
                UpdatedAt = DateTime.Now
            };
            File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, jsonOptions));
        }

        var configPath = Path.Combine(workspace.Path, "config.json");
        if (!File.Exists(configPath))
        {
            File.WriteAllText(configPath, JsonSerializer.Serialize(new AppConfig(), jsonOptions));
        }
    }

    private static WorkspaceContext ToContext(WorkspaceInfo workspace)
    {
        return new WorkspaceContext
        {
            Id = workspace.Id,
            Name = workspace.Name,
            RootPath = workspace.Path
        };
    }

    private string CreateNextEnglishWorkspaceId(IReadOnlyCollection<WorkspaceInfo> existingWorkspaces)
    {
        var index = 1;
        while (true)
        {
            var id = $"work{index}";
            var path = Path.Combine(WorkspacesDirectory, id);
            var idExists = existingWorkspaces.Any(workspace =>
                string.Equals(workspace.Id, id, StringComparison.OrdinalIgnoreCase));
            if (!idExists && !Directory.Exists(path))
            {
                return id;
            }

            index++;
        }
    }
}
