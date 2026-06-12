namespace VSLoader.Models;

public sealed class BatchImportRule
{
    public string MatchType { get; set; } = string.Empty;

    public string Pattern { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ModulePattern { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    public string NameTemplate { get; set; } = string.Empty;

    public int SortIndex { get; set; }

    public bool IsSimpleModuleMapRule => !string.IsNullOrWhiteSpace(ModuleName);
}
