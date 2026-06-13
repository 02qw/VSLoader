using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;
using System.Text.Json;

namespace VSLoader.Tests;

public sealed class WorkspaceSelectorViewModelTests : IDisposable
{
    private readonly string _rootPath;
    private readonly AppSettingsService _appSettingsService;
    private readonly WorkspaceService _workspaceService;
    private readonly AppSettings _settings;

    public WorkspaceSelectorViewModelTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _appSettingsService = new AppSettingsService(_rootPath);
        _workspaceService = new WorkspaceService(_rootPath);
        _settings = new AppSettings();
    }

    [Fact]
    public void Constructor_selects_last_workspace()
    {
        var first = _workspaceService.CreateWorkspace(_settings, "产线A");
        var second = _workspaceService.CreateWorkspace(_settings, "产线B");
        _settings.LastWorkspaceId = second.Id;

        var viewModel = CreateViewModel();

        Assert.NotNull(viewModel.SelectedWorkspace);
        Assert.Equal(second.Id, viewModel.SelectedWorkspace.Id);
        Assert.Contains(viewModel.Workspaces, item => item.Id == first.Id);
    }

    [Fact]
    public void Constructor_selects_first_workspace_when_last_workspace_is_missing()
    {
        var first = _workspaceService.CreateWorkspace(_settings, "产线A");
        _settings.LastWorkspaceId = "missing";

        var viewModel = CreateViewModel();

        Assert.NotNull(viewModel.SelectedWorkspace);
        Assert.Equal(first.Id, viewModel.SelectedWorkspace.Id);
    }

    [Fact]
    public void OpenSelectedWorkspace_sets_selected_context_and_saves_last_workspace()
    {
        var created = _workspaceService.CreateWorkspace(_settings, "产线A");
        var viewModel = CreateViewModel();
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == created.Id);

        viewModel.OpenSelectedWorkspaceCommand.Execute(null);
        var loaded = _appSettingsService.LoadOrCreate(out _);

        Assert.NotNull(viewModel.SelectedWorkspaceContext);
        Assert.Equal(created.Id, viewModel.SelectedWorkspaceContext.Id);
        Assert.Equal(created.Id, loaded.LastWorkspaceId);
    }

    [Fact]
    public void CreateWorkspace_adds_workspace_and_selects_it()
    {
        var viewModel = CreateViewModel();

        var result = viewModel.CreateWorkspace("产线A");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Single(viewModel.Workspaces);
        Assert.Equal("work1", viewModel.SelectedWorkspace?.Id);
        Assert.Equal("产线A", viewModel.SelectedWorkspace?.Name);
        Assert.Single(_settings.Workspaces);
    }

    [Fact]
    public void CreateWorkspace_rejects_duplicate_name()
    {
        var viewModel = CreateViewModel();
        Assert.True(viewModel.CreateWorkspace("产线A").Success);

        var result = viewModel.CreateWorkspace("  产线A  ");

        Assert.False(result.Success);
        Assert.Contains("已存在", result.ErrorMessage);
        Assert.Single(viewModel.Workspaces);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Line:A")]
    [InlineData("Line/A")]
    public void CreateWorkspace_rejects_invalid_name(string name)
    {
        var viewModel = CreateViewModel();

        var result = viewModel.CreateWorkspace(name);

        Assert.False(result.Success);
        Assert.Empty(viewModel.Workspaces);
    }

    [Fact]
    public void RenameSelectedWorkspace_updates_app_settings_name()
    {
        var created = _workspaceService.CreateWorkspace(_settings, "产线A");
        var viewModel = CreateViewModel();
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == created.Id);

        var result = viewModel.RenameSelectedWorkspace("产线B");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("产线B", _settings.Workspaces.Single(workspace => workspace.Id == created.Id).Name);
    }

    [Fact]
    public void RenameSelectedWorkspace_saves_app_settings()
    {
        var created = _workspaceService.CreateWorkspace(_settings, "产线A");
        var viewModel = CreateViewModel();
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == created.Id);

        var result = viewModel.RenameSelectedWorkspace("产线B");
        var loaded = _appSettingsService.LoadOrCreate(out _);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("产线B", loaded.Workspaces.Single(workspace => workspace.Id == created.Id).Name);
    }

    [Fact]
    public void RenameSelectedWorkspace_updates_workspace_metadata()
    {
        var created = _workspaceService.CreateWorkspace(_settings, "产线A");
        var info = _settings.Workspaces.Single(workspace => workspace.Id == created.Id);
        var viewModel = CreateViewModel();
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == created.Id);

        var result = viewModel.RenameSelectedWorkspace("产线B");
        var metadataJson = File.ReadAllText(Path.Combine(info.Path, "workspace.json"));
        var metadata = JsonSerializer.Deserialize<WorkspaceMetadata>(metadataJson);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(metadata);
        Assert.Equal("产线B", metadata.Name);
    }

    [Fact]
    public void RenameSelectedWorkspace_rejects_duplicate_name_excluding_current_workspace()
    {
        var first = _workspaceService.CreateWorkspace(_settings, "产线A");
        var second = _workspaceService.CreateWorkspace(_settings, "产线B");
        var viewModel = CreateViewModel();
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == second.Id);

        var result = viewModel.RenameSelectedWorkspace("  产线A  ");

        Assert.False(result.Success);
        Assert.Contains("已存在", result.ErrorMessage);
        Assert.Equal("产线B", _settings.Workspaces.Single(workspace => workspace.Id == second.Id).Name);
        Assert.Equal("产线A", _settings.Workspaces.Single(workspace => workspace.Id == first.Id).Name);
    }

    [Fact]
    public void RenameSelectedWorkspace_allows_same_name_for_current_workspace()
    {
        var created = _workspaceService.CreateWorkspace(_settings, "产线A");
        var info = _settings.Workspaces.Single(workspace => workspace.Id == created.Id);
        var originalPath = info.Path;
        var viewModel = CreateViewModel();
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == created.Id);

        var result = viewModel.RenameSelectedWorkspace("  产线A  ");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("产线A", info.Name);
        Assert.Equal(originalPath, info.Path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Line:A")]
    [InlineData("Line/A")]
    public void RenameSelectedWorkspace_rejects_invalid_name(string name)
    {
        var created = _workspaceService.CreateWorkspace(_settings, "产线A");
        var viewModel = CreateViewModel();
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == created.Id);

        var result = viewModel.RenameSelectedWorkspace(name);

        Assert.False(result.Success);
        Assert.Equal("产线A", _settings.Workspaces.Single(workspace => workspace.Id == created.Id).Name);
    }

    [Fact]
    public void RenameSelectedWorkspace_keeps_selected_workspace_after_rename()
    {
        var created = _workspaceService.CreateWorkspace(_settings, "产线A");
        var viewModel = CreateViewModel();
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == created.Id);

        var result = viewModel.RenameSelectedWorkspace("产线B");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(viewModel.SelectedWorkspace);
        Assert.Equal(created.Id, viewModel.SelectedWorkspace.Id);
        Assert.Equal("产线B", viewModel.SelectedWorkspace.Name);
    }

    [Fact]
    public void DeleteSelectedWorkspace_removes_workspace_from_app_settings()
    {
        var first = _workspaceService.CreateWorkspace(_settings, "产线A");
        var second = _workspaceService.CreateWorkspace(_settings, "产线B");
        var viewModel = CreateViewModel(activeWorkspaceId: first.Id);
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == second.Id);

        var result = viewModel.DeleteSelectedWorkspace();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.DoesNotContain(_settings.Workspaces, workspace => workspace.Id == second.Id);
        Assert.Contains(_settings.Workspaces, workspace => workspace.Id == first.Id);
    }

    [Fact]
    public void DeleteSelectedWorkspace_saves_app_settings()
    {
        var first = _workspaceService.CreateWorkspace(_settings, "产线A");
        var second = _workspaceService.CreateWorkspace(_settings, "产线B");
        var viewModel = CreateViewModel(activeWorkspaceId: first.Id);
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == second.Id);

        var result = viewModel.DeleteSelectedWorkspace();
        var loaded = _appSettingsService.LoadOrCreate(out _);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.DoesNotContain(loaded.Workspaces, workspace => workspace.Id == second.Id);
    }

    [Fact]
    public void DeleteSelectedWorkspace_deletes_workspace_directory()
    {
        var first = _workspaceService.CreateWorkspace(_settings, "产线A");
        var second = _workspaceService.CreateWorkspace(_settings, "产线B");
        var deletedInfo = _settings.Workspaces.Single(workspace => workspace.Id == second.Id);
        var deletedPath = deletedInfo.Path;
        var viewModel = CreateViewModel(activeWorkspaceId: first.Id);
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == second.Id);

        var result = viewModel.DeleteSelectedWorkspace();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(Directory.Exists(deletedPath));
    }

    [Fact]
    public void DeleteSelectedWorkspace_rejects_active_workspace()
    {
        var first = _workspaceService.CreateWorkspace(_settings, "产线A");
        _workspaceService.CreateWorkspace(_settings, "产线B");
        var viewModel = CreateViewModel(activeWorkspaceId: first.Id);
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == first.Id);

        var result = viewModel.DeleteSelectedWorkspace();

        Assert.False(result.Success);
        Assert.Contains("当前正在使用", result.ErrorMessage);
        Assert.Contains(_settings.Workspaces, workspace => workspace.Id == first.Id);
        Assert.True(Directory.Exists(first.RootPath));
    }

    [Fact]
    public void DeleteSelectedWorkspace_rejects_last_workspace()
    {
        var only = _workspaceService.CreateWorkspace(_settings, "产线A");
        var viewModel = CreateViewModel();
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == only.Id);

        var result = viewModel.DeleteSelectedWorkspace();

        Assert.False(result.Success);
        Assert.Contains("最后一个工作区", result.ErrorMessage);
        Assert.Contains(_settings.Workspaces, workspace => workspace.Id == only.Id);
        Assert.True(Directory.Exists(only.RootPath));
    }

    [Fact]
    public void DeleteSelectedWorkspace_allows_default_workspace_when_not_active_and_not_last()
    {
        var defaultContext = _workspaceService.EnsureDefaultWorkspace(_settings);
        var customContext = _workspaceService.CreateWorkspace(_settings, "产线A");
        var defaultPath = defaultContext.RootPath;
        var viewModel = CreateViewModel(activeWorkspaceId: customContext.Id);
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == defaultContext.Id);

        var result = viewModel.DeleteSelectedWorkspace();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.DoesNotContain(_settings.Workspaces, workspace => workspace.Id == "default");
        Assert.False(Directory.Exists(defaultPath));
    }

    [Fact]
    public void DeleteSelectedWorkspace_updates_last_workspace_when_deleted_workspace_was_last_workspace()
    {
        var first = _workspaceService.CreateWorkspace(_settings, "产线A");
        var second = _workspaceService.CreateWorkspace(_settings, "产线B");
        _settings.LastWorkspaceId = second.Id;
        var viewModel = CreateViewModel(activeWorkspaceId: first.Id);
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == second.Id);

        var result = viewModel.DeleteSelectedWorkspace();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(first.Id, _settings.LastWorkspaceId);
    }

    [Fact]
    public void DeleteSelectedWorkspace_refreshes_workspace_list_after_delete()
    {
        var first = _workspaceService.CreateWorkspace(_settings, "产线A");
        var second = _workspaceService.CreateWorkspace(_settings, "产线B");
        var viewModel = CreateViewModel(activeWorkspaceId: first.Id);
        viewModel.SelectedWorkspace = viewModel.Workspaces.Single(item => item.Id == second.Id);

        var result = viewModel.DeleteSelectedWorkspace();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.DoesNotContain(viewModel.Workspaces, workspace => workspace.Id == second.Id);
        Assert.Equal(first.Id, viewModel.SelectedWorkspace?.Id);
    }

    private WorkspaceSelectorViewModel CreateViewModel(string? activeWorkspaceId = null)
    {
        return new WorkspaceSelectorViewModel(_settings, _appSettingsService, _workspaceService, activeWorkspaceId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
