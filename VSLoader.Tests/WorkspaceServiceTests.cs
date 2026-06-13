using VSLoader.Models;
using VSLoader.Services;
using System.Text.Json;

namespace VSLoader.Tests;

public sealed class WorkspaceServiceTests : IDisposable
{
    private readonly string _rootPath;

    public WorkspaceServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void EnsureDefaultWorkspace_creates_workspace_files_and_registers_settings()
    {
        var settings = new AppSettings();
        var service = new WorkspaceService(_rootPath);

        var context = service.EnsureDefaultWorkspace(settings);

        Assert.Equal("default", context.Id);
        Assert.Equal("默认工作区", context.Name);
        Assert.True(Directory.Exists(context.RootPath));
        Assert.True(File.Exists(context.MetadataPath));
        Assert.True(File.Exists(context.ConfigPath));
        Assert.True(Directory.Exists(context.UiDownloadDirectory));
        Assert.Single(settings.Workspaces);
        Assert.Equal("default", settings.LastWorkspaceId);
    }

    [Fact]
    public void ResolveStartupWorkspace_returns_last_workspace_when_usable()
    {
        var settings = new AppSettings();
        var service = new WorkspaceService(_rootPath);
        var defaultContext = service.EnsureDefaultWorkspace(settings);
        var customContext = service.CreateWorkspace(settings, "产线A");
        settings.LastWorkspaceId = customContext.Id;

        var resolved = service.ResolveStartupWorkspace(settings);

        Assert.Equal(customContext.Id, resolved.Id);
        Assert.Equal(customContext.RootPath, resolved.RootPath);
        Assert.NotEqual(defaultContext.Id, resolved.Id);
    }

    [Fact]
    public void WorkspaceContext_calculates_expected_paths()
    {
        var rootPath = Path.Combine(_rootPath, "Workspaces", "LineA");
        var context = new WorkspaceContext
        {
            Id = "line-a",
            Name = "产线A",
            RootPath = rootPath
        };

        Assert.Equal(Path.Combine(rootPath, "workspace.json"), context.MetadataPath);
        Assert.Equal(Path.Combine(rootPath, "config.json"), context.ConfigPath);
        Assert.Equal(Path.Combine(rootPath, "window-layout.json"), context.WindowLayoutPath);
        Assert.Equal(Path.Combine(rootPath, "factory-map.layout.json"), context.FactoryMapLayoutPath);
        Assert.Equal(Path.Combine(rootPath, "UIdownload"), context.UiDownloadDirectory);
    }

    [Fact]
    public void ResolveWorkspace_returns_context_for_usable_workspace()
    {
        var settings = new AppSettings();
        var service = new WorkspaceService(_rootPath);
        var created = service.CreateWorkspace(settings, "产线A");
        var info = settings.Workspaces.Single(workspace => workspace.Id == created.Id);

        var resolved = service.ResolveWorkspace(info);

        Assert.Equal(created.Id, resolved.Id);
        Assert.Equal(created.Name, resolved.Name);
        Assert.Equal(created.RootPath, resolved.RootPath);
    }

    [Fact]
    public void ResolveWorkspace_throws_when_workspace_directory_is_missing()
    {
        var service = new WorkspaceService(_rootPath);
        var info = new WorkspaceInfo
        {
            Id = "missing",
            Name = "缺失工作区",
            Path = Path.Combine(_rootPath, "Workspaces", "Missing")
        };

        var exception = Assert.Throws<InvalidOperationException>(() => service.ResolveWorkspace(info));

        Assert.Contains("工作区不可用", exception.Message);
    }

    [Fact]
    public void CreateWorkspace_uses_english_work_id_instead_of_display_name()
    {
        var settings = new AppSettings();
        var service = new WorkspaceService(_rootPath);

        var context = service.CreateWorkspace(settings, "六七线");

        Assert.Equal("work1", context.Id);
        Assert.Equal("六七线", context.Name);
        Assert.EndsWith(Path.Combine("Workspaces", "work1"), context.RootPath);
        Assert.True(Directory.Exists(context.RootPath));
        Assert.DoesNotContain("六七线", context.RootPath);
    }

    [Fact]
    public void CreateWorkspace_skips_existing_work_ids()
    {
        var settings = new AppSettings();
        var service = new WorkspaceService(_rootPath);

        var first = service.CreateWorkspace(settings, "产线A");
        var second = service.CreateWorkspace(settings, "产线B");

        Assert.Equal("work1", first.Id);
        Assert.Equal("work2", second.Id);
        Assert.Equal(2, settings.Workspaces.Count);
    }

    [Fact]
    public void CreateWorkspace_skips_existing_work_directories()
    {
        var settings = new AppSettings();
        var service = new WorkspaceService(_rootPath);
        Directory.CreateDirectory(Path.Combine(service.WorkspacesDirectory, "work1"));

        var context = service.CreateWorkspace(settings, "新线");

        Assert.Equal("work2", context.Id);
        Assert.EndsWith(Path.Combine("Workspaces", "work2"), context.RootPath);
    }

    [Fact]
    public void EnsureDefaultWorkspace_does_not_overwrite_existing_last_workspace()
    {
        var settings = new AppSettings();
        var service = new WorkspaceService(_rootPath);
        var customContext = service.CreateWorkspace(settings, "产线A");
        settings.LastWorkspaceId = customContext.Id;

        service.EnsureDefaultWorkspace(settings);

        Assert.Equal(customContext.Id, settings.LastWorkspaceId);
    }

    [Fact]
    public void RenameWorkspace_updates_workspace_info_name_without_changing_path()
    {
        var settings = new AppSettings();
        var service = new WorkspaceService(_rootPath);
        var context = service.CreateWorkspace(settings, "产线A");
        var info = settings.Workspaces.Single(workspace => workspace.Id == context.Id);
        var originalPath = info.Path;

        var result = service.RenameWorkspace(info, "产线B");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("产线B", info.Name);
        Assert.Equal(context.Id, info.Id);
        Assert.Equal(originalPath, info.Path);
        Assert.True(Directory.Exists(originalPath));
    }

    [Fact]
    public void RenameWorkspace_updates_workspace_json_name()
    {
        var settings = new AppSettings();
        var service = new WorkspaceService(_rootPath);
        var context = service.CreateWorkspace(settings, "产线A");
        var info = settings.Workspaces.Single(workspace => workspace.Id == context.Id);

        var result = service.RenameWorkspace(info, "产线B");
        var metadataJson = File.ReadAllText(Path.Combine(info.Path, "workspace.json"));
        var metadata = JsonSerializer.Deserialize<WorkspaceMetadata>(metadataJson);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(metadata);
        Assert.Equal(info.Id, metadata.Id);
        Assert.Equal("产线B", metadata.Name);
    }

    [Fact]
    public void RenameWorkspace_returns_failure_when_workspace_directory_missing()
    {
        var service = new WorkspaceService(_rootPath);
        var info = new WorkspaceInfo
        {
            Id = "missing",
            Name = "缺失工作区",
            Path = Path.Combine(_rootPath, "Workspaces", "Missing")
        };

        var result = service.RenameWorkspace(info, "新名称");

        Assert.False(result.Success);
        Assert.Contains("工作区不可用", result.ErrorMessage);
    }

    [Fact]
    public void DeleteWorkspace_deletes_workspace_directory_recursively()
    {
        var settings = new AppSettings();
        var service = new WorkspaceService(_rootPath);
        var context = service.CreateWorkspace(settings, "产线A");
        var info = settings.Workspaces.Single(workspace => workspace.Id == context.Id);
        var nestedDirectory = Path.Combine(info.Path, "UIdownload", "Nested");
        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllText(Path.Combine(nestedDirectory, "sample.jnlp"), "content");

        var result = service.DeleteWorkspace(info);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(Directory.Exists(info.Path));
    }

    [Fact]
    public void DeleteWorkspace_returns_failure_when_workspace_directory_missing()
    {
        var service = new WorkspaceService(_rootPath);
        var info = new WorkspaceInfo
        {
            Id = "missing",
            Name = "缺失工作区",
            Path = Path.Combine(_rootPath, "Workspaces", "Missing")
        };

        var result = service.DeleteWorkspace(info);

        Assert.False(result.Success);
        Assert.Contains("工作区文件夹不存在", result.ErrorMessage);
    }

    [Fact]
    public void DeleteWorkspace_returns_failure_when_workspace_path_is_empty()
    {
        var service = new WorkspaceService(_rootPath);
        var info = new WorkspaceInfo
        {
            Id = "empty",
            Name = "空路径工作区",
            Path = string.Empty
        };

        var result = service.DeleteWorkspace(info);

        Assert.False(result.Success);
        Assert.Contains("工作区路径为空", result.ErrorMessage);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
