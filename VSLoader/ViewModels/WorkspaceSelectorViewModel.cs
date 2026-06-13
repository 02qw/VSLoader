using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.ViewModels;

public sealed partial class WorkspaceSelectorViewModel : ObservableObject
{
    private static readonly char[] InvalidNameChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];
    private readonly AppSettings appSettings;
    private readonly AppSettingsService appSettingsService;
    private readonly WorkspaceService workspaceService;
    private readonly string? activeWorkspaceId;

    public WorkspaceSelectorViewModel(
        AppSettings appSettings,
        AppSettingsService appSettingsService,
        WorkspaceService workspaceService,
        string? activeWorkspaceId = null)
    {
        this.appSettings = appSettings;
        this.appSettingsService = appSettingsService;
        this.workspaceService = workspaceService;
        this.activeWorkspaceId = activeWorkspaceId;
        RefreshWorkspaces();
    }

    public ObservableCollection<WorkspaceListItemViewModel> Workspaces { get; } = new();

    public WorkspaceContext? SelectedWorkspaceContext { get; private set; }

    public event Action<bool?>? RequestClose;

    public event Action? RequestCreateWorkspace;

    public event Action? RequestRenameWorkspace;

    public event Action? RequestDeleteWorkspace;

    public event Action<string>? ShowErrorRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedWorkspaceCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenWorkspaceFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartRenameWorkspaceCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartDeleteWorkspaceCommand))]
    private WorkspaceListItemViewModel? selectedWorkspace;

    [RelayCommand(CanExecute = nameof(CanOpenSelectedWorkspace))]
    private void OpenSelectedWorkspace()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        WorkspaceContext context;
        try
        {
            context = workspaceService.ResolveWorkspace(SelectedWorkspace.Info);
        }
        catch (Exception ex)
        {
            ShowErrorRequested?.Invoke(ex.Message);
            return;
        }

        appSettings.LastWorkspaceId = context.Id;
        var saveResult = appSettingsService.Save(appSettings);
        if (!saveResult.Success)
        {
            ShowErrorRequested?.Invoke($"保存程序配置失败：{saveResult.ErrorMessage}");
            return;
        }

        SelectedWorkspaceContext = context;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void StartCreateWorkspace()
    {
        RequestCreateWorkspace?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedWorkspace))]
    private void StartRenameWorkspace()
    {
        RequestRenameWorkspace?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedWorkspace))]
    private void StartDeleteWorkspace()
    {
        RequestDeleteWorkspace?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedWorkspace))]
    private void OpenWorkspaceFolder()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        if (!Directory.Exists(SelectedWorkspace.Path))
        {
            ShowErrorRequested?.Invoke("工作区文件夹不存在。");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedWorkspace.Path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowErrorRequested?.Invoke($"打开工作区文件夹失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    public SaveResult CreateWorkspace(string name)
    {
        var validationResult = ValidateWorkspaceName(name);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        var trimmedName = name.Trim();
        if (appSettings.Workspaces.Any(workspace =>
            string.Equals(workspace.Name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            return SaveResult.Fail("工作区名称已存在。");
        }

        try
        {
            var context = workspaceService.CreateWorkspace(appSettings, trimmedName);
            appSettings.LastWorkspaceId = context.Id;
            var saveResult = appSettingsService.Save(appSettings);
            if (!saveResult.Success)
            {
                return saveResult;
            }

            RefreshWorkspaces();
            SelectedWorkspace = Workspaces.FirstOrDefault(workspace => workspace.Id == context.Id);
            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
        }
    }

    public SaveResult RenameSelectedWorkspace(string newName)
    {
        if (SelectedWorkspace is null)
        {
            return SaveResult.Fail("请选择要重命名的工作区。");
        }

        var validationResult = ValidateWorkspaceName(newName);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        var selectedId = SelectedWorkspace.Id;
        var selectedInfo = SelectedWorkspace.Info;
        var trimmedName = newName.Trim();
        if (string.Equals(selectedInfo.Name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase))
        {
            return SaveResult.Ok();
        }

        if (appSettings.Workspaces.Any(workspace =>
            !string.Equals(workspace.Id, selectedId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(workspace.Name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            return SaveResult.Fail("工作区名称已存在。");
        }

        var renameResult = workspaceService.RenameWorkspace(selectedInfo, trimmedName);
        if (!renameResult.Success)
        {
            return renameResult;
        }

        var saveResult = appSettingsService.Save(appSettings);
        if (!saveResult.Success)
        {
            return saveResult;
        }

        RefreshWorkspaces();
        SelectedWorkspace = Workspaces.FirstOrDefault(workspace => workspace.Id == selectedId);
        return SaveResult.Ok();
    }

    public SaveResult DeleteSelectedWorkspace()
    {
        if (SelectedWorkspace is null)
        {
            return SaveResult.Fail("请选择要删除的工作区。");
        }

        if (appSettings.Workspaces.Count <= 1)
        {
            return SaveResult.Fail("至少需要保留一个工作区，不能删除最后一个工作区。");
        }

        var selectedId = SelectedWorkspace.Id;
        if (!string.IsNullOrWhiteSpace(activeWorkspaceId)
            && string.Equals(selectedId, activeWorkspaceId, StringComparison.OrdinalIgnoreCase))
        {
            return SaveResult.Fail("当前正在使用的工作区不能删除。请先切换到其他工作区后再删除。");
        }

        var selectedInfo = SelectedWorkspace.Info;
        var deleteResult = workspaceService.DeleteWorkspace(selectedInfo);
        if (!deleteResult.Success)
        {
            return deleteResult;
        }

        appSettings.Workspaces.RemoveAll(workspace =>
            string.Equals(workspace.Id, selectedId, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(appSettings.LastWorkspaceId, selectedId, StringComparison.OrdinalIgnoreCase))
        {
            appSettings.LastWorkspaceId = appSettings.Workspaces.FirstOrDefault(workspace =>
                    workspaceService.IsWorkspaceUsable(workspace))?.Id
                ?? appSettings.Workspaces.FirstOrDefault()?.Id
                ?? string.Empty;
        }

        var saveResult = appSettingsService.Save(appSettings);
        if (!saveResult.Success)
        {
            return saveResult;
        }

        RefreshWorkspaces();
        return SaveResult.Ok();
    }

    private bool CanOpenSelectedWorkspace()
    {
        return SelectedWorkspace is { IsUsable: true };
    }

    private bool HasSelectedWorkspace()
    {
        return SelectedWorkspace is not null;
    }

    private void RefreshWorkspaces()
    {
        Workspaces.Clear();
        foreach (var workspace in appSettings.Workspaces)
        {
            Workspaces.Add(new WorkspaceListItemViewModel(
                workspace,
                string.Equals(workspace.Id, appSettings.LastWorkspaceId, StringComparison.OrdinalIgnoreCase),
                workspaceService.IsWorkspaceUsable(workspace)));
        }

        SelectedWorkspace = Workspaces.FirstOrDefault(workspace =>
                string.Equals(workspace.Id, appSettings.LastWorkspaceId, StringComparison.OrdinalIgnoreCase))
            ?? Workspaces.FirstOrDefault(workspace => workspace.IsUsable)
            ?? Workspaces.FirstOrDefault();
    }

    private static SaveResult ValidateWorkspaceName(string name)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return SaveResult.Fail("请输入有效的工作区名称。");
        }

        if (trimmedName.Length > 60)
        {
            return SaveResult.Fail("工作区名称不能超过 60 个字符。");
        }

        if (trimmedName.IndexOfAny(InvalidNameChars) >= 0)
        {
            return SaveResult.Fail("工作区名称不能包含：\\ / : * ? \" < > |");
        }

        return SaveResult.Ok();
    }
}
