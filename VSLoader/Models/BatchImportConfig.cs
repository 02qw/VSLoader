namespace VSLoader.Models;

public sealed class BatchImportConfig
{
    public string LastParentFolderPath { get; set; } = string.Empty;

    public string LastCsvPath { get; set; } = string.Empty;

    public BatchImportConfig Clone()
    {
        return new BatchImportConfig
        {
            LastParentFolderPath = LastParentFolderPath,
            LastCsvPath = LastCsvPath
        };
    }
}
