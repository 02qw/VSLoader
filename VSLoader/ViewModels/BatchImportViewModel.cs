using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.ViewModels;

public sealed partial class BatchImportViewModel : ObservableObject
{
    private readonly IReadOnlyList<ShortcutItem> _existingShortcuts;
    private readonly DialogService _dialogService;
    private readonly BatchImportService _batchImportService;

    public BatchImportViewModel(
        IEnumerable<ShortcutItem> existingShortcuts,
        DialogService dialogService,
        BatchImportService batchImportService,
        BatchImportConfig? initialConfig = null)
    {
        _existingShortcuts = existingShortcuts.ToList();
        _dialogService = dialogService;
        _batchImportService = batchImportService;

        if (initialConfig is not null)
        {
            ParentFolderPath = initialConfig.LastParentFolderPath ?? string.Empty;
            CsvPath = initialConfig.LastCsvPath ?? string.Empty;
        }
    }

    [ObservableProperty]
    private string parentFolderPath = string.Empty;

    [ObservableProperty]
    private string csvPath = string.Empty;

    [ObservableProperty]
    private int scannedCount;

    [ObservableProperty]
    private int importableCount;

    [ObservableProperty]
    private int skippedCount;

    [ObservableProperty]
    private int duplicateCount;

    [ObservableProperty]
    private int ruleErrorCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmImportCommand))]
    private int selectedImportCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BrowseParentFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCsvCommand))]
    [NotifyCanExecuteChangedFor(nameof(ScanPreviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
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

    public ObservableCollection<BatchImportPreviewItem> PreviewItems { get; } = new();

    public IReadOnlyList<ShortcutItem> ImportedShortcuts { get; private set; } = Array.Empty<ShortcutItem>();

    public IReadOnlyList<BatchImportApplyItem> ApplyItems { get; private set; } = Array.Empty<BatchImportApplyItem>();

    public bool HasSuccessfulScan { get; private set; }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    [RelayCommand(CanExecute = nameof(CanRunWindowCommand))]
    private void BrowseParentFolder()
    {
        var path = _dialogService.SelectFolder();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ParentFolderPath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunWindowCommand))]
    private void BrowseCsv()
    {
        var path = _dialogService.SelectCsvFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            CsvPath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunWindowCommand))]
    private async Task ScanPreview()
    {
        var parentPath = ParentFolderPath.Trim();
        var csvPath = CsvPath.Trim();

        if (string.IsNullOrWhiteSpace(parentPath))
        {
            _dialogService.ShowError("请填写目标父级文件夹路径。");
            return;
        }

        if (!Directory.Exists(parentPath))
        {
            _dialogService.ShowError("目标父级路径不存在或不可访问。");
            return;
        }

        if (string.IsNullOrWhiteSpace(csvPath))
        {
            _dialogService.ShowError("请选择 CSV 规则文件。");
            return;
        }

        if (!File.Exists(csvPath))
        {
            _dialogService.ShowError("CSV 文件不存在。");
            return;
        }

        IsBusy = true;
        BusyMessage = "正在扫描预览，请稍候...";
        BusyProgressValue = 0;
        BusyProgressMaximum = 1;
        BusyProgressText = "准备扫描...";
        BusyCurrentItemText = string.Empty;

        try
        {
            var progress = new Progress<BatchImportScanProgress>(scanProgress =>
            {
                BusyProgressValue = scanProgress.CompletedCount;
                BusyProgressMaximum = Math.Max(1, scanProgress.TotalCount);
                BusyProgressText = scanProgress.TotalCount > 0
                    ? $"当前进度：{scanProgress.CompletedCount} / {scanProgress.TotalCount}"
                    : scanProgress.Stage;
                BusyCurrentItemText = string.IsNullOrWhiteSpace(scanProgress.CurrentFolderName)
                    ? scanProgress.Stage
                    : $"{scanProgress.Stage}：{scanProgress.CurrentFolderName}";
            });

            var scanResult = await Task.Run(() =>
            {
                var rules = _batchImportService.LoadRules(csvPath, out var ruleErrors);
                if (HasFatalRuleErrors(ruleErrors))
                {
                    return BatchImportScanResult.CreateFailure(ruleErrors);
                }

                var previewItems = _batchImportService.BuildPreview(parentPath, rules, _existingShortcuts, ruleErrors, progress);
                return BatchImportScanResult.CreateSuccess(previewItems);
            });

            if (!scanResult.Success)
            {
                _dialogService.ShowError(string.Join(Environment.NewLine, scanResult.Errors));
                return;
            }

            ReplacePreviewItems(scanResult.PreviewItems);

            if (!scanResult.PreviewItems.Any(item => !string.IsNullOrWhiteSpace(item.TargetPath)))
            {
                _dialogService.ShowInfo("未扫描到子文件夹。");
            }

            RefreshStatistics();
            HasSuccessfulScan = true;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"扫描预览失败：{ex.Message}");
        }
        finally
        {
            ClearBusyState();
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmImport))]
    private void ConfirmImport()
    {
        ApplyItems = _batchImportService.CreateApplyItems(PreviewItems);
        ImportedShortcuts = ApplyItems
            .Where(item => !item.IsUpdate)
            .Select(item => item.Shortcut)
            .ToList();
        RequestClose?.Invoke(true);
    }

    [RelayCommand(CanExecute = nameof(CanRunWindowCommand))]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    public event Action<bool?>? RequestClose;

    private bool CanConfirmImport()
    {
        return !IsBusy && SelectedImportCount > 0;
    }

    private bool CanRunWindowCommand()
    {
        return !IsBusy;
    }

    private static bool HasFatalRuleErrors(IEnumerable<string> ruleErrors)
    {
        return ruleErrors.Any(error => error.Contains("CSV 表头不正确", StringComparison.OrdinalIgnoreCase)
            || error.Contains("CSV 读取失败", StringComparison.OrdinalIgnoreCase));
    }

    private void ReplacePreviewItems(IEnumerable<BatchImportPreviewItem> previewItems)
    {
        foreach (var oldItem in PreviewItems)
        {
            oldItem.PropertyChanged -= PreviewItem_PropertyChanged;
        }

        PreviewItems.Clear();
        foreach (var item in previewItems)
        {
            item.PropertyChanged += PreviewItem_PropertyChanged;
            PreviewItems.Add(item);
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
    }

    private void PreviewItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BatchImportPreviewItem.IsSelected))
        {
            RefreshStatistics();
        }
    }

    private void RefreshStatistics()
    {
        ScannedCount = PreviewItems.Count(item => item.Status != BatchImportService.StatusRuleError);
        ImportableCount = PreviewItems.Count(item => item.Status == BatchImportService.StatusImportable);
        SkippedCount = PreviewItems.Count(item => item.Status == BatchImportService.StatusSkipped);
        DuplicateCount = PreviewItems.Count(item => item.Status == BatchImportService.StatusDuplicate);
        RuleErrorCount = PreviewItems.Count(item => item.Status == BatchImportService.StatusRuleError);
        SelectedImportCount = PreviewItems.Count(item => item.CanImport && item.IsSelected);
    }

    private sealed record BatchImportScanResult(
        bool Success,
        IReadOnlyList<BatchImportPreviewItem> PreviewItems,
        IReadOnlyList<string> Errors)
    {
        public static BatchImportScanResult CreateSuccess(IReadOnlyList<BatchImportPreviewItem> previewItems)
        {
            return new BatchImportScanResult(true, previewItems, Array.Empty<string>());
        }

        public static BatchImportScanResult CreateFailure(IReadOnlyList<string> errors)
        {
            return new BatchImportScanResult(false, Array.Empty<BatchImportPreviewItem>(), errors);
        }
    }
}
