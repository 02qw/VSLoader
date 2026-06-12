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
    private readonly WebUiService _webUiService;
    private readonly ShortcutSearchService _shortcutSearchService;
    private readonly PasswordProtectionService _passwordProtectionService;
    private readonly ClipboardService _clipboardService;
    private AppConfig _config = new();
    private bool _configLoadFailed;
    private bool _hasInvalidConfigFile;
    private int _statusMessageVersion;

    public MainViewModel()
        : this(new ConfigService(), new VSCodeLauncherService(), new DialogService(), new BatchImportService(), new AdminUiService(), new WebUiService(), new ShortcutSearchService(), new PasswordProtectionService(), new ClipboardService())
    {
    }

    public MainViewModel(
        ConfigService configService,
        VSCodeLauncherService launcherService,
        DialogService dialogService,
        BatchImportService batchImportService,
        AdminUiService adminUiService,
        WebUiService webUiService,
        ShortcutSearchService shortcutSearchService,
        PasswordProtectionService passwordProtectionService,
        ClipboardService clipboardService)
    {
        _configService = configService;
        _launcherService = launcherService;
        _dialogService = dialogService;
        _batchImportService = batchImportService;
        _adminUiService = adminUiService;
        _webUiService = webUiService;
        _shortcutSearchService = shortcutSearchService;
        _passwordProtectionService = passwordProtectionService;
        _clipboardService = clipboardService;
        ShortcutsView = CollectionViewSource.GetDefaultView(Shortcuts);
        ShortcutsView.Filter = FilterShortcut;
        SetCustomSort(ShortcutSortField.Name, ListSortDirection.Ascending);
        LoadConfig();
    }

    public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();

    public ICollectionView ShortcutsView { get; }

    public event EventHandler? ShortcutsChanged;

    public HotkeyConfig CurrentHotkey => _config.Hotkey;

    public Func<HotkeyConfig, SaveResult>? TryRegisterHotkey { get; set; }

    public ShortcutSortField CurrentSortField { get; private set; } = ShortcutSortField.Name;

    public ListSortDirection? CurrentSortDirection { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenAdminUiCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadSelectedAdminUiLinkCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenWebUiCommand))]
    private ShortcutItem? selectedShortcut;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string shortcutCountText = "0 / 0";

    [ObservableProperty]
    private bool hasStatusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenBatchImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadAdminUiLinksCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenAdminUiCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadSelectedAdminUiLinkCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenWebUiCommand))]
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
        RefreshShortcutsView();
    }

    public void ApplySort(ShortcutSortField field)
    {
        if (CurrentSortDirection is null || CurrentSortField != field)
        {
            CurrentSortField = field;
            CurrentSortDirection = ListSortDirection.Ascending;
            SetCustomSort(CurrentSortField, CurrentSortDirection.Value);
            return;
        }

        if (CurrentSortDirection == ListSortDirection.Ascending)
        {
            CurrentSortDirection = ListSortDirection.Descending;
            SetCustomSort(CurrentSortField, CurrentSortDirection.Value);
            return;
        }

        CurrentSortField = ShortcutSortField.Name;
        CurrentSortDirection = null;
        SetCustomSort(ShortcutSortField.Name, ListSortDirection.Ascending);
    }

    public void ApplyDefaultSort()
    {
        CurrentSortField = ShortcutSortField.Name;
        CurrentSortDirection = null;
        SetCustomSort(ShortcutSortField.Name, ListSortDirection.Ascending);
    }

    public ListSortDirection EffectiveSortDirection
    {
        get
        {
            return CurrentSortDirection ?? ListSortDirection.Ascending;
        }
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
            RefreshShortcutsView();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunGlobalCommand))]
    private void OpenBatchImport()
    {
        var viewModel = new BatchImportViewModel(Shortcuts, _dialogService, _batchImportService, _config.BatchImport.Clone());
        var window = new BatchImportWindow(viewModel);
        var dialogResult = window.ShowDialog();
        var shouldSaveConfig = false;

        if (viewModel.HasSuccessfulScan)
        {
            _config.BatchImport.LastParentFolderPath = viewModel.ParentFolderPath.Trim();
            _config.BatchImport.LastCsvPath = viewModel.CsvPath.Trim();
            shouldSaveConfig = true;
        }

        if (dialogResult == true)
        {
            var importedCount = 0;
            var updatedCount = 0;
            var cleanupCount = 0;
            foreach (var applyItem in viewModel.ApplyItems)
            {
                if (applyItem.IsUpdate)
                {
                    if (TryUpdateExistingShortcut(applyItem))
                    {
                        updatedCount++;
                    }

                    cleanupCount += RemoveDuplicateShortcuts(applyItem);
                }
                else
                {
                    Shortcuts.Add(applyItem.Shortcut);
                    importedCount++;
                }
            }

            shouldSaveConfig = true;
            RefreshShortcutsView();

            if (importedCount > 0 || updatedCount > 0 || cleanupCount > 0)
            {
                _dialogService.ShowInfo(BuildBatchImportResultMessage(importedCount, updatedCount, cleanupCount));
            }
        }

        if (shouldSaveConfig)
        {
            SaveCurrentConfig();
        }
    }

    private bool TryUpdateExistingShortcut(BatchImportApplyItem applyItem)
    {
        var existingPathKey = BatchImportService.NormalizePathKey(applyItem.ExistingTargetPath);
        var existingShortcut = applyItem.ExistingShortcutToUpdate is not null && Shortcuts.Contains(applyItem.ExistingShortcutToUpdate)
            ? applyItem.ExistingShortcutToUpdate
            : Shortcuts.FirstOrDefault(shortcut =>
            string.Equals(BatchImportService.NormalizePathKey(shortcut.TargetPath), existingPathKey, StringComparison.OrdinalIgnoreCase));

        if (existingShortcut is null)
        {
            Shortcuts.Add(applyItem.Shortcut);
            return false;
        }

        existingShortcut.Name = applyItem.Shortcut.Name;
        existingShortcut.TargetPath = applyItem.Shortcut.TargetPath;
        existingShortcut.Description = applyItem.Shortcut.Description;
        existingShortcut.UpdatedAt = applyItem.Shortcut.UpdatedAt;
        return true;
    }

    private int RemoveDuplicateShortcuts(BatchImportApplyItem applyItem)
    {
        var removedCount = 0;
        foreach (var duplicateShortcut in applyItem.DuplicateShortcutsToRemove)
        {
            if (Shortcuts.Remove(duplicateShortcut))
            {
                removedCount++;
                continue;
            }

            var fallbackMatch = Shortcuts.FirstOrDefault(shortcut =>
                string.Equals(BatchImportService.NormalizePathKey(shortcut.TargetPath), BatchImportService.NormalizePathKey(duplicateShortcut.TargetPath), StringComparison.OrdinalIgnoreCase)
                && string.Equals(shortcut.Name.Trim(), duplicateShortcut.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                && shortcut.UpdatedAt == duplicateShortcut.UpdatedAt);

            if (fallbackMatch is not null && Shortcuts.Remove(fallbackMatch))
            {
                removedCount++;
            }
        }

        return removedCount;
    }

    private static string BuildBatchImportResultMessage(int importedCount, int updatedCount, int cleanupCount)
    {
        var messageParts = new List<string>();
        if (importedCount > 0)
        {
            messageParts.Add($"已新增 {importedCount} 个快捷项");
        }

        if (updatedCount > 0)
        {
            messageParts.Add($"已更新 {updatedCount} 个快捷项");
        }

        if (cleanupCount > 0)
        {
            messageParts.Add($"已清理 {cleanupCount} 个重复快捷项");
        }

        return string.Join("，", messageParts) + "。";
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

            var shortcutSnapshot = Shortcuts.ToList();
            var adminUiConfig = _config.AdminUi.Clone();

            var testResult = await _adminUiService.TestConnectionAsync(adminUiConfig);
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

            var result = await Task.Run(async () =>
            {
                return await _adminUiService.DownloadAllAsync(shortcutSnapshot, adminUiConfig, progress);
            });
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
    private async Task DownloadSelectedAdminUiLinkAsync()
    {
        if (SelectedShortcut is null)
        {
            return;
        }

        try
        {
            var shortcut = SelectedShortcut;
            var adminUiConfig = _config.AdminUi.Clone();

            IsBusy = true;
            BusyMessage = $"正在获取 {shortcut.Name} 的 AdminUI 连接，请稍候...";
            BusyProgressValue = 0;
            BusyProgressMaximum = 1;
            BusyProgressText = "正在测试 AdminUI 网络连接...";
            BusyCurrentItemText = string.Empty;

            var testResult = await _adminUiService.TestConnectionAsync(adminUiConfig);
            if (!testResult.Success)
            {
                BusyProgressText = "网络连接失败。";
                _dialogService.ShowError(testResult.ErrorMessage ?? "网络连接失败，请检查 AdminUI BaseUrl、网络环境或 VPN。");
                return;
            }

            BusyProgressText = "正在下载 AdminUI 连接...";
            BusyCurrentItemText = $"正在处理：{shortcut.Name}";

            var result = await Task.Run(async () =>
            {
                return await _adminUiService.DownloadOneAsync(shortcut, adminUiConfig);
            });

            if (!result.Success)
            {
                _dialogService.ShowError(result.ErrorMessage ?? "获取 AdminUI 连接失败。");
                return;
            }

            BusyProgressValue = 1;
            BusyProgressText = "获取完成。";
            _dialogService.ShowInfo($"已获取 AdminUI 连接：{shortcut.Name}");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"获取 AdminUI 连接失败：{ex.Message}");
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
    private async Task OpenAdminUiAsync()
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

        var clipboardResult = await _clipboardService.SetTextWithRetryAsync(password);
        if (clipboardResult.Success)
        {
            ShowTemporaryStatusMessage("AdminUI 已打开，密码已复制到剪贴板。");
            return;
        }

        _dialogService.ShowError($"AdminUI 已打开，但写入剪贴板失败：{clipboardResult.ErrorMessage}");
    }

    [RelayCommand(CanExecute = nameof(HasSelectedShortcut))]
    private void OpenWebUi()
    {
        if (SelectedShortcut is null)
        {
            return;
        }

        var result = _webUiService.OpenWebUi(SelectedShortcut, _config.WebUi);
        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "打开 WebUI 失败。");
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
            RefreshShortcutsView();
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
        RefreshShortcutsView();
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
        var viewModel = new SettingsViewModel(_config.VSCodePath, _config.AdminUi, _config.Hotkey, _dialogService, _passwordProtectionService, TryRegisterHotkey);
        var window = new SettingsWindow(viewModel);

        if (window.ShowDialog() == true)
        {
            _config.VSCodePath = viewModel.VSCodePath.Trim();
            _config.AdminUi = viewModel.AdminUi.Clone();
            _config.Hotkey = viewModel.Hotkey.Clone();
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

    private void SetCustomSort(ShortcutSortField field, ListSortDirection direction)
    {
        if (ShortcutsView is ListCollectionView listCollectionView)
        {
            listCollectionView.CustomSort = new ShortcutSortService(field, direction);
        }

        RefreshShortcutsView();
    }

    private void LoadConfig()
    {
        var result = _configService.Load();
        _config = result.Config;
        _configLoadFailed = !result.Success;
        _hasInvalidConfigFile = result.HasInvalidConfigFile;

        Shortcuts.Clear();
        foreach (var shortcut in _config.Shortcuts)
        {
            Shortcuts.Add(shortcut);
        }

        RefreshShortcutsView();
        UpdateStatusMessage();
    }

    private void RefreshShortcutsView()
    {
        ShortcutsView.Refresh();
        UpdateShortcutCountText();
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateShortcutCountText()
    {
        var visibleCount = ShortcutsView.OfType<ShortcutItem>().Count();
        var totalCount = Shortcuts.Count;
        ShortcutCountText = $"{visibleCount} / {totalCount}";
    }

    private void SaveCurrentConfig()
    {
        _config.Shortcuts = Shortcuts.ToList();
        if (_hasInvalidConfigFile)
        {
            var backupResult = _configService.BackupInvalidConfigFile();
            if (!backupResult.Success)
            {
                _dialogService.ShowError($"备份损坏配置文件失败：{backupResult.ErrorMessage}");
                return;
            }

            _hasInvalidConfigFile = false;
        }

        var result = _configService.Save(_config);
        if (!result.Success)
        {
            _dialogService.ShowError($"保存配置失败：{result.ErrorMessage}");
            return;
        }

        _configLoadFailed = false;
        UpdateStatusMessage();
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

        return _shortcutSearchService.IsTextMatch(shortcut.Name, keyword)
            || Contains(shortcut.TargetPath, keyword)
            || _shortcutSearchService.IsTextMatch(shortcut.Description, keyword);
    }

    private static bool Contains(string source, string keyword)
    {
        return source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
