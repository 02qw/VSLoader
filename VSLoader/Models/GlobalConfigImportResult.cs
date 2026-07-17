namespace VSLoader.Models;

public sealed class GlobalConfigImportResult
{
    private GlobalConfigImportResult(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public List<string> Warnings { get; } = new();

    public List<string> ImportedItems { get; } = new();

    public bool HasInvalidVSCodePath { get; set; }

    public bool RequiresMapWindowReload { get; set; }

    public bool RequiresWindowLayoutReload { get; set; }

    public static GlobalConfigImportResult Ok()
    {
        return new GlobalConfigImportResult(true, null);
    }

    public static GlobalConfigImportResult Fail(string errorMessage)
    {
        return new GlobalConfigImportResult(false, errorMessage);
    }
}
