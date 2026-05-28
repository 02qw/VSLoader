using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.ViewModels;

public sealed partial class ShortcutEditViewModel : ObservableObject
{
    private readonly IEnumerable<ShortcutItem> _existingShortcuts;
    private readonly ShortcutItem? _editingShortcut;
    private readonly DialogService _dialogService;

    public ShortcutEditViewModel(IEnumerable<ShortcutItem> existingShortcuts, ShortcutItem? editingShortcut, DialogService dialogService)
    {
        _existingShortcuts = existingShortcuts;
        _editingShortcut = editingShortcut;
        _dialogService = dialogService;
        IsEditMode = editingShortcut is not null;

        if (editingShortcut is not null)
        {
            Name = editingShortcut.Name;
            TargetPath = editingShortcut.TargetPath;
            Description = editingShortcut.Description;
        }
    }

    public bool IsEditMode { get; }

    public string WindowTitle => IsEditMode ? "编辑快捷项" : "新增快捷项";

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string targetPath = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    public ShortcutItem? Result { get; private set; }

    [RelayCommand]
    private void BrowseFolder()
    {
        var path = _dialogService.SelectFolder();
        if (!string.IsNullOrWhiteSpace(path))
        {
            TargetPath = path;
        }
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var path = _dialogService.SelectFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            TargetPath = path;
        }
    }

    [RelayCommand]
    private void Save()
    {
        var normalizedName = Name.Trim();
        var normalizedTargetPath = TargetPath.Trim();
        var normalizedDescription = Description.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            _dialogService.ShowError("名称不能为空。");
            return;
        }

        if (string.IsNullOrWhiteSpace(normalizedTargetPath))
        {
            _dialogService.ShowError("目标路径不能为空。");
            return;
        }

        if (IsDuplicateName(normalizedName))
        {
            _dialogService.ShowError("快捷项名称不能重复。");
            return;
        }

        if (!VSCodeLauncherService.IsNetworkPath(normalizedTargetPath) && !VSCodeLauncherService.PathExists(normalizedTargetPath))
        {
            _dialogService.ShowError("本地目标路径不存在。");
            return;
        }

        var now = DateTime.Now;
        Result = new ShortcutItem
        {
            Name = normalizedName,
            TargetPath = normalizedTargetPath,
            Description = normalizedDescription,
            CreatedAt = _editingShortcut?.CreatedAt ?? now,
            UpdatedAt = IsEditMode ? now : now
        };

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    public event Action<bool?>? RequestClose;

    private bool IsDuplicateName(string normalizedName)
    {
        return _existingShortcuts.Any(shortcut =>
            !ReferenceEquals(shortcut, _editingShortcut)
            && string.Equals(shortcut.Name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));
    }
}
