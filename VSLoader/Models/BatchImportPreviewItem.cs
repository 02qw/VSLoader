using CommunityToolkit.Mvvm.ComponentModel;

namespace VSLoader.Models;

public sealed partial class BatchImportPreviewItem : ObservableObject
{
    public string FolderName { get; set; } = string.Empty;

    public string TargetPath { get; set; } = string.Empty;

    public string GeneratedName { get; set; } = string.Empty;

    public string MatchedPattern { get; set; } = string.Empty;

    public string ExistingTargetPath { get; set; } = string.Empty;

    public string ExistingName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool CanImport { get; set; }

    public bool IsUpdate { get; set; }

    public int SortRuleIndex { get; set; } = int.MaxValue;

    public int? SortNo { get; set; }

    public string SortName { get; set; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}
