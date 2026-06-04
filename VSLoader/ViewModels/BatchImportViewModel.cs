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

    public ObservableCollection<BatchImportPreviewItem> PreviewItems { get; } = new();

    public IReadOnlyList<ShortcutItem> ImportedShortcuts { get; private set; } = Array.Empty<ShortcutItem>();

    public bool HasSuccessfulScan { get; private set; }

    [RelayCommand]
    private void BrowseParentFolder()
    {
        var path = _dialogService.SelectFolder();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ParentFolderPath = path;
        }
    }

    [RelayCommand]
    private void BrowseCsv()
    {
        var path = _dialogService.SelectCsvFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            CsvPath = path;
        }
    }

    [RelayCommand]
    private void ScanPreview()
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

        var rules = _batchImportService.LoadRules(csvPath, out var ruleErrors);
        if (ruleErrors.Any(error => error.Contains("CSV 表头不正确", StringComparison.OrdinalIgnoreCase)
            || error.Contains("CSV 读取失败", StringComparison.OrdinalIgnoreCase)))
        {
            _dialogService.ShowError(string.Join(Environment.NewLine, ruleErrors));
            return;
        }

        var previewItems = _batchImportService.BuildPreview(parentPath, rules, _existingShortcuts, ruleErrors);

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

        if (!Directory.EnumerateDirectories(parentPath).Any())
        {
            _dialogService.ShowInfo("未扫描到子文件夹。");
        }

        RefreshStatistics();
        HasSuccessfulScan = true;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmImport))]
    private void ConfirmImport()
    {
        ImportedShortcuts = _batchImportService.CreateShortcuts(PreviewItems);
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    public event Action<bool?>? RequestClose;

    private bool CanConfirmImport()
    {
        return SelectedImportCount > 0;
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
}
