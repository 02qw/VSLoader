using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VSLoader.Models;
using VSLoader.Services;
using VSLoader.Views;

namespace VSLoader.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan TemporaryStatusDuration = TimeSpan.FromSeconds(3);
    private readonly ConfigService _configService;
    private readonly VSCodeLauncherService _launcherService;
    private readonly DialogService _dialogService;
    private readonly BatchImportService _batchImportService;
    private readonly AdminUiService _adminUiService;
    private readonly PasswordProtectionService _passwordProtectionService;
    private AppConfig _config = new();
    private bool _configLoadFailed;
    private int _statusMessageVersion;

    public MainViewModel()
        : this(new ConfigService(), new VSCodeLauncherService(), new DialogService(), new BatchImportService(), new AdminUiService(), new PasswordProtectionService())
    {
    }

    public MainViewModel(
        ConfigService configService,
        VSCodeLauncherService launcherService,
        DialogService dialogService,
        BatchImportService batchImportService,
        AdminUiService adminUiService,
        PasswordProtectionService passwordProtectionService)
    {
        _configService = configService;
        _launcherService = launcherService;
        _dialogService = dialogService;
        _batchImportService = batchImportService;
        _adminUiService = adminUiService;
        _passwordProtectionService = passwordProtectionService;
        ShortcutsView = CollectionViewSource.GetDefaultView(Shortcuts);
        ShortcutsView.Filter = FilterShortcut;
        if (ShortcutsView is ListCollectionView listCollectionView)
        {
            listCollectionView.CustomSort = new ShortcutSortService();
        }
        LoadConfig();
    }

    public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();

    public ICollectionView ShortcutsView { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenAdminUiCommand))]
    private ShortcutItem? selectedShortcut;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool hasStatusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenBatchImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadAdminUiLinksCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenAdminUiCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenSettingsCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string busyMessage = string.Empty;

    [ObservableProperty]
    private int busyProgressValue;

    [ObservableProperty]
    private int busyProgressMaximum;

    [ObservableProperty]
    private string busyProgressText = string.Empty;

    [ObservableProperty]
    private string busyCurrentItemText = string.Empty;

    public bool IsNotBusy => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    partial void OnSearchTextChanged(string value)
    {
        ShortcutsView.Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanRunGlobalCommand))]
    private void AddShortcut()
    {
        var viewModel = new ShortcutEditViewModel(Shortcuts, null, _dialogService);
        var window = new ShortcutEditWindow(viewModel);

        if (window.ShowDialog() == true && viewModel.Result is not null)
        {
            Shortcuts.Add(viewModel.Result);
            SaveCurrentConfig();
            ShortcutsView.Refresh();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunGlobalCommand))]
    private void OpenBatchImport()
    {
        var viewModel = new BatchImportViewModel(Shortcuts, _dialogService, _batchImportService);
        var window = new BatchImportWindow(viewModel);

        if (window.ShowDialog() == true)
        {
            var importedShortcuts = viewModel.ImportedShortcuts;
            foreach (var shortcut in importedShortcuts)
            {
                Shortcuts.Add(shortcut);
            }

            SaveCurrentConfig();
            ShortcutsView.Refresh();

            if (importedShortcuts.Count > 0)
            {
                _dialogService.ShowInfo($"已新增 {importedShortcuts.Count} 个快捷项。");
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunGlobalCommand))]
    private async Task DownloadAdminUiLinksAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (Shortcuts.Count == 0)
        {
            _dialogService.ShowInfo("当前没有快捷项可处理。");
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "正在自动获取 AdminUI 连接，请稍候...";
            BusyProgressValue = 0;
            BusyProgressMaximum = Shortcuts.Count;
            BusyProgressText = "正在测试 AdminUI 网络连接...";
            BusyCurrentItemText = string.Empty;

            var testResult = await _adminUiService.TestConnectionAsync(_config.AdminUi);
            if (!testResult.Success)
            {
                BusyProgressText = "网络连接失败。";
                BusyCurrentItemText = string.Empty;
                _dialogService.ShowError(testResult.ErrorMessage ?? "网络连接失败，请检查 AdminUI BaseUrl、网络环境或 VPN。");
                return;
            }

            var progress = new Progress<AdminUiDownloadProgress>(downloadProgress =>
            {
                BusyProgressValue = downloadProgress.CompletedCount;
                BusyProgressMaximum = downloadProgress.TotalCount;
                BusyProgressText = $"当前进度：{downloadProgress.CompletedCount} / {downloadProgress.TotalCount}，成功：{downloadProgress.SuccessCount}，失败：{downloadProgress.FailedCount}";
                BusyCurrentItemText = string.IsNullOrWhiteSpace(downloadProgress.CurrentShortcutName)
                    ? string.Empty
                    : $"正在处理：{downloadProgress.CurrentShortcutName}";
            });

            var result = await _adminUiService.DownloadAllAsync(Shortcuts, _config.AdminUi, progress);
            var message = $"自动获取连接完成。\n成功：{result.SuccessCount}\n失败：{result.FailedCount}";

            if (result.Messages.Count > 0)
            {
                message += "\n\n" + string.Join("\n", result.Messages.Take(10));
                if (result.Messages.Count > 10)
                {
                    message += $"\n... 还有 {result.Messages.Count - 10} 条消息。";
                }
            }

            _dialogService.ShowInfo(message);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"自动获取连接失败：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
            BusyProgressValue = 0;
            BusyProgressMaximum = 0;
            BusyProgressText = string.Empty;
            BusyCurrentItemText = string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedShortcut))]
    private void OpenAdminUi()
    {
        if (SelectedShortcut is null)
        {
            return;
        }

        var result = _adminUiService.OpenAdminUi(SelectedShortcut, _config.AdminUi);
        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "打开 AdminUI 失败。");
            return;
        }

        var password = _passwordProtectionService.Unprotect(_config.AdminUi.ProtectedPassword);
        if (string.IsNullOrEmpty(password))
        {
            ShowTemporaryStatusMessage("AdminUI 已打开，但未配置 AdminUI 密码。");
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(password);
            ShowTemporaryStatusMessage("AdminUI 已打开，密码已复制到剪贴板。");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"AdminUI 已打开，但写入剪贴板失败：{ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedShortcut))]
    private void EditShortcut()
    {
        if (SelectedShortcut is null)
        {
            return;
        }

        var viewModel = new ShortcutEditViewModel(Shortcuts, SelectedShortcut, _dialogService);
        var window = new ShortcutEditWindow(viewModel);

        if (window.ShowDialog() == true && viewModel.Result is not null)
        {
            SelectedShortcut.Name = viewModel.Result.Name;
            SelectedShortcut.TargetPath = viewModel.Result.TargetPath;
            SelectedShortcut.Description = viewModel.Result.Description;
            SelectedShortcut.UpdatedAt = DateTime.Now;
            SaveCurrentConfig();
            ShortcutsView.Refresh();
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedShortcut))]
    private void DeleteShortcut()
    {
        if (SelectedShortcut is null)
        {
            return;
        }

        if (!_dialogService.Confirm("确定要删除该快捷项吗？"))
        {
            return;
        }

        Shortcuts.Remove(SelectedShortcut);
        SelectedShortcut = null;
        SaveCurrentConfig();
        ShortcutsView.Refresh();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedShortcut))]
    private void OpenShortcut()
    {
        if (SelectedShortcut is null)
        {
            return;
        }

        var result = _launcherService.Launch(_config.VSCodePath, SelectedShortcut.TargetPath);
        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "打开 VSCode 失败。");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunGlobalCommand))]
    private void OpenSettings()
    {
        var viewModel = new SettingsViewModel(_config.VSCodePath, _config.AdminUi, _dialogService, _passwordProtectionService);
        var window = new SettingsWindow(viewModel);

        if (window.ShowDialog() == true)
        {
            _config.VSCodePath = viewModel.VSCodePath.Trim();
            _config.AdminUi = viewModel.AdminUi.Clone();
            SaveCurrentConfig();
            _configLoadFailed = false;
            UpdateStatusMessage();
        }
    }

    private bool HasSelectedShortcut()
    {
        return !IsBusy && SelectedShortcut is not null;
    }

    private bool CanRunGlobalCommand()
    {
        return !IsBusy;
    }

    private void LoadConfig()
    {
        var result = _configService.Load();
        _config = result.Config;
        _configLoadFailed = !result.Success;

        Shortcuts.Clear();
        foreach (var shortcut in _config.Shortcuts)
        {
            Shortcuts.Add(shortcut);
        }

        if (!result.Success)
        {
            _dialogService.ShowError($"配置文件读取失败，请检查 {_configService.ConfigPath}。\n\n{result.ErrorMessage}");
        }

        UpdateStatusMessage();
    }

    private void SaveCurrentConfig()
    {
        _config.Shortcuts = Shortcuts.ToList();
        var result = _configService.Save(_config);
        if (!result.Success)
        {
            _dialogService.ShowError($"保存配置失败：{result.ErrorMessage}");
        }
    }

    private void UpdateStatusMessage()
    {
        if (_configLoadFailed)
        {
            ShowStatusMessage($"配置文件读取失败，请检查 {_configService.ConfigPath}。");
            return;
        }

        if (!VSCodeLauncherService.IsValidExecutablePath(_config.VSCodePath))
        {
            ShowStatusMessage("尚未配置有效的 VSCode 路径，请进入设置。");
            return;
        }

        StatusMessage = string.Empty;
        HasStatusMessage = false;
    }

    private void ShowStatusMessage(string message)
    {
        _statusMessageVersion++;
        StatusMessage = message;
        HasStatusMessage = true;
    }

    private async void ShowTemporaryStatusMessage(string message)
    {
        var version = ++_statusMessageVersion;
        StatusMessage = message;
        HasStatusMessage = true;

        await Task.Delay(TemporaryStatusDuration);

        if (_statusMessageVersion == version)
        {
            StatusMessage = string.Empty;
            HasStatusMessage = false;
        }
    }

    private bool FilterShortcut(object item)
    {
        if (item is not ShortcutItem shortcut)
        {
            return false;
        }

        var keyword = SearchText.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            return true;
        }

        return Contains(shortcut.Name, keyword)
            || Contains(shortcut.TargetPath, keyword)
            || Contains(shortcut.Description, keyword);
    }

    private static bool Contains(string source, string keyword)
    {
        return source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
