using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
    private readonly AppSettings _appSettings;
    private readonly AppSettingsService _appSettingsService;
    private readonly VSCodeLauncherService _launcherService;
    private readonly DialogService _dialogService;
    private readonly BatchImportService _batchImportService;
    private readonly AdminUiService _adminUiService;
    private readonly WebUiService _webUiService;
    private readonly ShortcutSearchService _shortcutSearchService;
    private readonly PasswordProtectionService _passwordProtectionService;
    private readonly ClipboardService _clipboardService;
    private readonly AdminUiAutoPasteService _adminUiAutoPasteService;
    private readonly AdminUiAutoPasteLogService _adminUiAutoPasteLogService;
    private readonly UpdateCheckService _updateCheckService;
    private readonly string _updateTimePath;
    private readonly SoftwareUpdateService _softwareUpdateService;
    private readonly string _softwareUpdatesRoot;
    private readonly GlobalConfigPackageService _globalConfigPackageService;
    private readonly VSCodePathResolver _vsCodePathResolver;
    private readonly UpdaterRunnerService _updaterRunnerService;
    private readonly string _factoryMapLayoutPath;
    private AppConfig _config = new();
    private bool _configLoadFailed;
    private bool _hasInvalidConfigFile;
    private int _statusMessageVersion;
    private CancellationTokenSource? _updateCheckCancellationTokenSource;
    private Task? _updateCheckLoopTask;
    private UpdateCheckResult? _lastUpdateCheckResult;

    public MainViewModel()
        : this(new AppSettings(), new AppSettingsService(), new ConfigService(), new VSCodeLauncherService(), new DialogService(), new BatchImportService(), new AdminUiService(), new WebUiService(), new ShortcutSearchService(), new PasswordProtectionService(), new ClipboardService())
    {
    }

    public MainViewModel(
        AppSettings appSettings,
        AppSettingsService appSettingsService,
        ConfigService configService,
        VSCodeLauncherService launcherService,
        DialogService dialogService,
        BatchImportService batchImportService,
        AdminUiService adminUiService,
        WebUiService webUiService,
        ShortcutSearchService shortcutSearchService,
        PasswordProtectionService passwordProtectionService,
        ClipboardService clipboardService,
        UpdateCheckService? updateCheckService = null,
        string? updateTimePath = null,
        SoftwareUpdateService? softwareUpdateService = null,
        string? softwareUpdatesRoot = null,
        GlobalConfigPackageService? globalConfigPackageService = null,
        VSCodePathResolver? vsCodePathResolver = null,
        UpdaterRunnerService? updaterRunnerService = null,
        AdminUiAutoPasteService? adminUiAutoPasteService = null,
        string? factoryMapLayoutPath = null,
        AdminUiAutoPasteLogService? adminUiAutoPasteLogService = null)
    {
        _appSettings = appSettings;
        _appSettingsService = appSettingsService;
        _configService = configService;
        _launcherService = launcherService;
        _dialogService = dialogService;
        _batchImportService = batchImportService;
        _adminUiService = adminUiService;
        _webUiService = webUiService;
        _shortcutSearchService = shortcutSearchService;
        _passwordProtectionService = passwordProtectionService;
        _clipboardService = clipboardService;
        _adminUiAutoPasteService = adminUiAutoPasteService ?? new AdminUiAutoPasteService();
        _adminUiAutoPasteLogService = adminUiAutoPasteLogService ?? new AdminUiAutoPasteLogService();
        _updateCheckService = updateCheckService ?? new UpdateCheckService();
        _updateTimePath = string.IsNullOrWhiteSpace(updateTimePath)
            ? Path.Combine(_configService.ConfigDirectory, "updateTime.json")
            : updateTimePath;
        _softwareUpdateService = softwareUpdateService ?? new SoftwareUpdateService();
        _softwareUpdatesRoot = string.IsNullOrWhiteSpace(softwareUpdatesRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSLoader", "Updates")
            : softwareUpdatesRoot;
        _globalConfigPackageService = globalConfigPackageService ?? new GlobalConfigPackageService();
        _vsCodePathResolver = vsCodePathResolver ?? new VSCodePathResolver();
        _updaterRunnerService = updaterRunnerService ?? new UpdaterRunnerService();
        _factoryMapLayoutPath = string.IsNullOrWhiteSpace(factoryMapLayoutPath)
            ? Path.Combine(_configService.ConfigDirectory, "factory-map.layout.json")
            : factoryMapLayoutPath;
        ShortcutsView = CollectionViewSource.GetDefaultView(Shortcuts);
        ShortcutsView.Filter = FilterShortcut;
        SetCustomSort(ShortcutSortField.Name, ListSortDirection.Ascending);
        LoadConfig();
    }

    public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();

    public ICollectionView ShortcutsView { get; }

    public event EventHandler? ShortcutsChanged;

    public HotkeyConfig CurrentHotkey => _config.Hotkey;

    public MapHotkeyConfig CurrentMapHotkey => _config.MapHotkey;

    public Func<HotkeyConfig, MapHotkeyConfig, SaveResult>? TryRegisterHotkeys { get; set; }

    public Func<string, string, bool> StartUpdater { get; set; } = static (path, arguments) =>
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(path),
            UseShellExecute = true
        });

        return process is not null;
    };

    public Func<string> GetAppBaseDirectory { get; set; } = static () => AppContext.BaseDirectory;

    public Action RequestApplicationExit { get; set; } = static () => System.Windows.Application.Current.Shutdown();

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
    private string updateNoticeMessage = string.Empty;

    [ObservableProperty]
    private bool hasUpdateNotice;

    [ObservableProperty]
    private bool hasSoftwareUpdateNotice;

    [ObservableProperty]
    private string updateFailureMessage = string.Empty;

    [ObservableProperty]
    private bool hasUpdateFailure;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddShortcutCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSoftwareCommand))]
    [NotifyCanExecuteChangedFor(nameof(ManualCheckUpdatesCommand))]
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

    [ObservableProperty]
    private BusyOverlayHost busyOverlayHost = BusyOverlayHost.Main;

    public bool IsNotBusy => !IsBusy;

    public bool IsMainBusyOverlayVisible => IsBusy && BusyOverlayHost == BusyOverlayHost.Main;

    public bool IsMapBusyOverlayVisible => IsBusy && BusyOverlayHost == BusyOverlayHost.Map;

    internal static bool ShouldShowNoUpdateStatus(UpdateCheckResult result)
    {
        return result.UpdatedItems.Count == 0 && result.Failures.Count == 0;
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        OnPropertyChanged(nameof(IsMainBusyOverlayVisible));
        OnPropertyChanged(nameof(IsMapBusyOverlayVisible));
    }

    partial void OnBusyOverlayHostChanged(BusyOverlayHost value)
    {
        OnPropertyChanged(nameof(IsMainBusyOverlayVisible));
        OnPropertyChanged(nameof(IsMapBusyOverlayVisible));
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshShortcutsView();
    }

    public void StartUpdateCheckLoop()
    {
        StopUpdateCheckLoop();
        _updateCheckCancellationTokenSource = new CancellationTokenSource();
        _updateCheckLoopTask = RunUpdateCheckLoopAsync(_updateCheckCancellationTokenSource.Token);
    }

    public bool IsUpdateCheckLoopRunning => _updateCheckLoopTask is { IsCompleted: false };

    public void StopUpdateCheckLoop()
    {
        if (_updateCheckCancellationTokenSource is null)
        {
            return;
        }

        _updateCheckCancellationTokenSource.Cancel();
        _updateCheckCancellationTokenSource.Dispose();
        _updateCheckCancellationTokenSource = null;
        if (_updateCheckLoopTask?.IsCompleted == true)
        {
            _updateCheckLoopTask = null;
        }
    }

    public async Task StopUpdateCheckLoopAsync(TimeSpan timeout)
    {
        var cts = _updateCheckCancellationTokenSource;
        var loopTask = _updateCheckLoopTask;
        if (cts is null || loopTask is null)
        {
            _updateCheckCancellationTokenSource = null;
            _updateCheckLoopTask = null;
            return;
        }

        cts.Cancel();
        try
        {
            await loopTask.WaitAsync(timeout);
        }
        catch (OperationCanceledException)
        {
        }
        catch (TimeoutException)
        {
        }
        catch
        {
        }
        finally
        {
            cts.Dispose();
            if (ReferenceEquals(_updateCheckCancellationTokenSource, cts))
            {
                _updateCheckCancellationTokenSource = null;
            }

            if (ReferenceEquals(_updateCheckLoopTask, loopTask))
            {
                _updateCheckLoopTask = null;
            }
        }
    }

    public void ApplyUpdateCheckResult(UpdateCheckResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.DetectedSoftwareVersion))
        {
            HasSoftwareUpdateNotice = true;
        }
        else if (result.Failures.Count == 0)
        {
            HasSoftwareUpdateNotice = false;
        }

        if (result.UpdatedItems.Count > 0)
        {
            _lastUpdateCheckResult = result;
            UpdateNoticeMessage = $"检测到更新：{string.Join("、", result.UpdatedItems)}";
            HasUpdateNotice = true;
        }
        else
        {
            _lastUpdateCheckResult = null;
            UpdateNoticeMessage = string.Empty;
            HasUpdateNotice = false;
        }

        if (result.Failures.Count > 0)
        {
            UpdateFailureMessage = $"更新检测失败：{string.Join("、", result.Failures)}";
            HasUpdateFailure = true;
        }
        else
        {
            UpdateFailureMessage = string.Empty;
            HasUpdateFailure = false;
        }
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

    [RelayCommand]
    private void CloseUpdateNotice()
    {
        if (_lastUpdateCheckResult is not null)
        {
            var acknowledgeResult = _updateCheckService.AcknowledgeDetectedUpdates(_updateTimePath, _lastUpdateCheckResult);
            if (!acknowledgeResult.Success)
            {
                UpdateFailureMessage = $"更新提醒确认失败：{acknowledgeResult.ErrorMessage}";
                HasUpdateFailure = true;
                return;
            }
        }

        _lastUpdateCheckResult = null;
        UpdateNoticeMessage = string.Empty;
        HasUpdateNotice = false;
        HasSoftwareUpdateNotice = false;
    }

    [RelayCommand]
    private void CloseUpdateFailure()
    {
        UpdateFailureMessage = string.Empty;
        HasUpdateFailure = false;
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
    private async Task UpdateSoftwareAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_appSettings.SoftwareUpdateManifestPath))
        {
            _dialogService.ShowError("请先进入设置配置软件更新 manifest 路径。");
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "正在检查软件版本，请稍候...";
            BusyProgressValue = 0;
            BusyProgressMaximum = 100;
            BusyProgressText = "正在读取 manifest...";
            BusyCurrentItemText = string.Empty;

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
            var manifestPath = _appSettings.SoftwareUpdateManifestPath.Trim();
            var availability = await _softwareUpdateService.CheckAvailabilityAsync(manifestPath, currentVersion);
            if (!availability.Success)
            {
                _dialogService.ShowError(availability.ErrorMessage);
                return;
            }

            if (!availability.UpdateAvailable)
            {
                HasSoftwareUpdateNotice = false;
                _dialogService.ShowInfo(availability.Message);
                return;
            }

            if (!_dialogService.Confirm("确定要更新 VSLoader 吗？\n更新过程中主程序会暂时关闭，并由更新器接管。"))
            {
                return;
            }

            BusyMessage = "正在启动更新器，请稍候...";
            BusyProgressText = "正在启动更新器...";
            var appBaseDirectory = GetAppBaseDirectory();
            var updaterPath = Path.Combine(appBaseDirectory, "VSLoader.Updater.exe");
            if (!File.Exists(updaterPath))
            {
                _dialogService.ShowError("当前程序目录缺少 VSLoader.Updater.exe，无法启动更新器。");
                return;
            }

            var runnerResult = _updaterRunnerService.Prepare(appBaseDirectory);
            if (!runnerResult.Success)
            {
                _dialogService.ShowError(runnerResult.ErrorMessage);
                return;
            }

            var arguments = BuildUpdaterArguments(
                manifestPath,
                currentVersion);

            if (!StartUpdater(runnerResult.RunnerUpdaterPath, arguments))
            {
                _dialogService.ShowError("更新器启动失败，主程序不会退出。");
                return;
            }

            RequestApplicationExit();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"软件更新失败：{ex.Message}");
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

    [RelayCommand(CanExecute = nameof(CanRunGlobalCommand))]
    private async Task ManualCheckUpdatesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            BusyOverlayHost = BusyOverlayHost.Main;
            BusyMessage = "正在检测更新...";
            BusyProgressValue = 0;
            BusyProgressMaximum = 0;
            BusyProgressText = string.Empty;
            BusyCurrentItemText = string.Empty;

            await StopUpdateCheckLoopAsync(TimeSpan.FromSeconds(2));
            var result = await CheckUpdatesOnceAsync(CancellationToken.None);
            if (ShouldShowNoUpdateStatus(result))
            {
                ShowTemporaryStatusMessage("已完成检测，当前没有发现更新。");
            }
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
            BusyProgressValue = 0;
            BusyProgressMaximum = 0;
            BusyProgressText = string.Empty;
            BusyCurrentItemText = string.Empty;
            StartUpdateCheckLoop();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunGlobalCommand))]
    private async Task ExportGlobalConfigAsync()
    {
        var defaultFileName = GlobalConfigPackageService.BuildDefaultExportFileName(DateTime.Now);
        var packagePath = _dialogService.SaveJsonFile(defaultFileName);
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "正在导出全局配置...";
            BusyProgressMaximum = 100;
            BusyProgressValue = 30;
            BusyProgressText = "正在整理当前工作区配置...";
            BusyCurrentItemText = string.Empty;

            _config.Shortcuts = Shortcuts.ToList();
            var result = _globalConfigPackageService.Export(packagePath, _config, _appSettings, _factoryMapLayoutPath);
            if (!result.Success)
            {
                _dialogService.ShowError(result.ErrorMessage ?? "全局配置导出失败。");
                return;
            }

            BusyProgressValue = 100;
            MarkExportedGlobalConfigUsedIfConfiguredPath(packagePath);
            _dialogService.ShowInfo(BuildGlobalConfigExportMessage(packagePath, result));
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"全局配置导出失败：{ex.Message}");
        }
        finally
        {
            ClearBusyState();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunGlobalCommand))]
    private async Task ImportGlobalConfigAsync()
    {
        var packagePath = _dialogService.SelectJsonFile();
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "正在导入全局配置...";
            BusyProgressMaximum = 100;
            BusyProgressValue = 20;
            BusyProgressText = "正在读取并校验配置包...";
            BusyCurrentItemText = string.Empty;

            var result = _globalConfigPackageService.Import(
                packagePath,
                _configService.ConfigPath,
                _factoryMapLayoutPath,
                _appSettings,
                _ => _vsCodePathResolver.Resolve());

            if (!result.Success)
            {
                _dialogService.ShowError(result.ErrorMessage ?? "全局配置导入失败。");
                return;
            }

            BusyProgressValue = 70;
            var saveSettingsResult = _appSettingsService.Save(_appSettings);
            if (!saveSettingsResult.Success)
            {
                _dialogService.ShowError($"保存程序配置失败：{saveSettingsResult.ErrorMessage}");
                return;
            }

            LoadConfig();
            TryRegisterImportedHotkey(result);
            MarkImportedGlobalConfigUsed(packagePath);
            BusyProgressValue = 100;
            ApplyGlobalConfigImportStatus(result);
            _dialogService.ShowInfo(BuildGlobalConfigImportMessage(result));
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"全局配置导入失败：{ex.Message}");
        }
        finally
        {
            ClearBusyState();
        }
    }

    private string BuildUpdaterArguments(string manifestPath, Version currentVersion)
    {
        var targetDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var builder = new System.Text.StringBuilder();
        AppendProcessArgument(builder, "--mode");
        AppendProcessArgument(builder, "update");
        AppendProcessArgument(builder, "--processId");
        AppendProcessArgument(builder, Environment.ProcessId.ToString());
        AppendProcessArgument(builder, "--targetDir");
        AppendProcessArgument(builder, targetDirectory);
        AppendProcessArgument(builder, "--mainExeName");
        AppendProcessArgument(builder, "VSLoader.exe");
        AppendProcessArgument(builder, "--manifestPath");
        AppendProcessArgument(builder, manifestPath);
        AppendProcessArgument(builder, "--currentVersion");
        AppendProcessArgument(builder, currentVersion.ToString());
        AppendProcessArgument(builder, "--updatesRoot");
        AppendProcessArgument(builder, _softwareUpdatesRoot);
        return builder.ToString().Trim();
    }

    private static void AppendProcessArgument(System.Text.StringBuilder builder, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append('"');
        builder.Append(value.Replace("\"", "\\\"", StringComparison.Ordinal));
        builder.Append('"');
    }

    private void TryRegisterImportedHotkey(GlobalConfigImportResult result)
    {
        if (TryRegisterHotkeys is null || (!_config.Hotkey.Enabled && !_config.MapHotkey.Enabled))
        {
            return;
        }

        var hotkeyResult = TryRegisterHotkeys(_config.Hotkey, _config.MapHotkey);
        if (!hotkeyResult.Success)
        {
            result.Warnings.Add($"快捷键注册失败：{hotkeyResult.ErrorMessage}");
        }
    }

    private void MarkExportedGlobalConfigUsedIfConfiguredPath(string packagePath)
    {
        if (!IsSamePath(packagePath, _config.UpdateCheck.GlobalConfigPackagePath))
        {
            return;
        }

        var markResult = _updateCheckService.MarkGlobalConfigUsed(packagePath, _updateTimePath);
        if (!markResult.Success)
        {
            ShowUpdateFailure($"全局配置基线更新失败：{markResult.ErrorMessage}");
        }
    }

    private void MarkImportedGlobalConfigUsed(string packagePath)
    {
        var markResult = _updateCheckService.MarkGlobalConfigUsed(packagePath, _updateTimePath);
        if (!markResult.Success)
        {
            ShowUpdateFailure($"全局配置基线更新失败：{markResult.ErrorMessage}");
        }
    }

    private static bool IsSamePath(string? firstPath, string? secondPath)
    {
        if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath))
        {
            return false;
        }

        try
        {
            var firstFullPath = Path.GetFullPath(firstPath.Trim());
            var secondFullPath = Path.GetFullPath(secondPath.Trim());
            return string.Equals(firstFullPath, secondFullPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void ApplyGlobalConfigImportStatus(GlobalConfigImportResult result)
    {
        if (result.HasInvalidVSCodePath)
        {
            ShowStatusMessage("导入完成，但未找到有效 VSCode 路径，请进入设置配置。");
            return;
        }

        if (result.Warnings.Count > 0)
        {
            ShowStatusMessage("配置导入完成，但部分路径无效，请检查设置。");
            return;
        }

        UpdateStatusMessage();
    }

    private static string BuildGlobalConfigExportMessage(string packagePath, GlobalConfigExportResult result)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("全局配置导出完成。");
        builder.AppendLine();
        builder.AppendLine($"文件：{packagePath}");
        if (result.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("提示：");
            AppendNumberedLines(builder, result.Warnings);
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildGlobalConfigImportMessage(GlobalConfigImportResult result)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(result.Warnings.Count > 0
            ? "全局配置导入完成，但存在需要检查的问题："
            : "全局配置导入完成。");

        if (result.ImportedItems.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("已导入：");
            AppendNumberedLines(builder, result.ImportedItems);
        }

        if (result.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("需要检查：");
            AppendNumberedLines(builder, result.Warnings);
        }

        if (result.RequiresMapWindowReload)
        {
            builder.AppendLine();
            builder.AppendLine("如果地图窗口已打开，请关闭后重新打开地图以应用新布局。");
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendNumberedLines(System.Text.StringBuilder builder, IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            builder.AppendLine($"{index + 1}. {lines[index]}");
        }
    }

    private void ClearBusyState()
    {
        IsBusy = false;
        BusyMessage = string.Empty;
        BusyProgressValue = 0;
        BusyProgressMaximum = 0;
        BusyProgressText = string.Empty;
        BusyCurrentItemText = string.Empty;
        BusyOverlayHost = BusyOverlayHost.Main;
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

        if (dialogResult == true)
        {
            var markResult = _updateCheckService.MarkRulesUsed(_config.UpdateCheck, _updateTimePath);
            if (!markResult.Success)
            {
                ShowUpdateFailure($"rules 基线更新失败：{markResult.ErrorMessage}");
            }
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
        existingShortcut.SourceModuleName = applyItem.Shortcut.SourceModuleName;
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

        var adminUiConfig = _config.AdminUi.Clone();
        var result = await _adminUiService.OpenAdminUiAsync(SelectedShortcut, adminUiConfig);
        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "打开 AdminUI 失败。");
            return;
        }

        var password = _passwordProtectionService.Unprotect(adminUiConfig.ProtectedPassword);
        if (string.IsNullOrEmpty(password))
        {
            ShowTemporaryStatusMessage("AdminUI 已打开，但未配置 AdminUI 密码。");
            return;
        }

        var clipboardResult = await _clipboardService.SetTextWithRetryAsync(password);
        if (clipboardResult.Success)
        {
            LogAdminUiClipboardCheck(password);
            if (!adminUiConfig.AutoPastePasswordEnabled)
            {
                ShowTemporaryStatusMessage("AdminUI 已打开，密码已复制到剪贴板。");
                return;
            }

            ShowTemporaryStatusMessage("AdminUI 已打开，密码已复制到剪贴板，正在等待 AdminUI 前台窗口...");
            var pasteResult = await _adminUiAutoPasteService.TryPasteAsync(adminUiConfig);
            if (pasteResult.Success)
            {
                ShowTemporaryStatusMessage("AdminUI 已打开，密码已自动粘贴并回车。");
                return;
            }

            ShowTemporaryStatusMessage($"AdminUI 已打开，密码已复制到剪贴板。{pasteResult.Message}请手动粘贴。");
            return;
        }

        _dialogService.ShowError($"AdminUI 已打开，但写入剪贴板失败：{clipboardResult.ErrorMessage}");
    }

    private void LogAdminUiClipboardCheck(string expectedText)
    {
        try
        {
            var clipboardText = System.Windows.Clipboard.ContainsText()
                ? System.Windows.Clipboard.GetText()
                : string.Empty;
            _adminUiAutoPasteLogService.LogClipboardCheck(
                expectedText.Length,
                clipboardText.Length,
                string.Equals(clipboardText, expectedText, StringComparison.Ordinal));
        }
        catch (Exception ex)
        {
            _adminUiAutoPasteLogService.LogError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedShortcut))]
    private async Task OpenWebUiAsync()
    {
        if (SelectedShortcut is null)
        {
            return;
        }

        var result = await _webUiService.OpenWebUiAsync(SelectedShortcut, _config.WebUi);
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

    internal void ApplyEditedShortcutFromMap(ShortcutItem shortcut, ShortcutItem editedShortcut)
    {
        shortcut.Name = editedShortcut.Name;
        shortcut.TargetPath = editedShortcut.TargetPath;
        shortcut.Description = editedShortcut.Description;
        shortcut.UpdatedAt = DateTime.Now;
        SaveCurrentConfig();
        RefreshShortcutsView();
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
    private async Task OpenShortcutAsync()
    {
        if (SelectedShortcut is null)
        {
            return;
        }

        var result = await _launcherService.LaunchAsync(_appSettings.VSCodePath, SelectedShortcut.TargetPath);
        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "打开 VSCode 失败。");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunGlobalCommand))]
    private void OpenSettings()
    {
        var viewModel = new SettingsViewModel(_appSettings.VSCodePath, _appSettings.SoftwareUpdateManifestPath, _config.AdminUi, _config.WebUi, _config.UpdateCheck, _config.Hotkey, _config.MapHotkey, _dialogService, _passwordProtectionService, TryRegisterHotkeys);
        var window = new SettingsWindow(viewModel);

        if (window.ShowDialog() == true)
        {
            _appSettings.VSCodePath = viewModel.VSCodePath.Trim();
            _appSettings.SoftwareUpdateManifestPath = viewModel.SoftwareUpdateManifestPath.Trim();
            var appSettingsSaveResult = _appSettingsService.Save(_appSettings);
            if (!appSettingsSaveResult.Success)
            {
                _dialogService.ShowError($"保存程序配置失败：{appSettingsSaveResult.ErrorMessage}");
                return;
            }

            _config.AdminUi = viewModel.AdminUi.Clone();
            _config.WebUi = viewModel.WebUi.Clone();
            _config.UpdateCheck = viewModel.UpdateCheck.Clone();
            _config.Hotkey = viewModel.Hotkey.Clone();
            _config.MapHotkey = viewModel.MapHotkey.Clone();
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

    private async Task RunUpdateCheckLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                await CheckUpdatesOnceAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<UpdateCheckResult> CheckUpdatesOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var updateCheckConfig = _config.UpdateCheck.Clone();
            var updateTimePath = _updateTimePath;
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
            var softwareUpdateManifestPath = _appSettings.SoftwareUpdateManifestPath?.Trim() ?? string.Empty;

            var result = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _updateCheckService.CheckAsync(updateCheckConfig, updateTimePath, currentVersion, softwareUpdateManifestPath, cancellationToken);
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return new UpdateCheckResult();
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ApplyUpdateCheckResult(result));
            return result;
        }
        catch (OperationCanceledException)
        {
            return new UpdateCheckResult();
        }
        catch (Exception ex)
        {
            var result = new UpdateCheckResult();
            result.Failures.Add(ex.Message);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ApplyUpdateCheckResult(result));
            return result;
        }
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

    public void MarkMapFileUsed(string mapFilePath)
    {
        var markResult = _updateCheckService.MarkMapUsed(mapFilePath, _updateTimePath);
        if (!markResult.Success)
        {
            ShowUpdateFailure($"map 基线更新失败：{markResult.ErrorMessage}");
        }
    }

    private void UpdateStatusMessage()
    {
        if (_configLoadFailed)
        {
            ShowStatusMessage($"配置文件读取失败，请检查 {_configService.ConfigPath}。");
            return;
        }

        if (!VSCodeLauncherService.IsValidExecutablePath(_appSettings.VSCodePath))
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

    private void ShowUpdateFailure(string message)
    {
        UpdateFailureMessage = string.IsNullOrWhiteSpace(UpdateFailureMessage)
            ? $"更新检测失败：{message}"
            : $"{UpdateFailureMessage}、{message}";
        HasUpdateFailure = true;
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
            || _shortcutSearchService.IsTextMatch(shortcut.Description, keyword)
            || _shortcutSearchService.IsTextMatch(shortcut.SourceModuleName, keyword);
    }

    private static bool Contains(string source, string keyword)
    {
        return source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
