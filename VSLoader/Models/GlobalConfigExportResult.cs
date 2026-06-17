namespace VSLoader.Models;

public sealed class GlobalConfigExportResult
{
    private GlobalConfigExportResult(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public List<string> Warnings { get; } = new();

    public static GlobalConfigExportResult Ok()
    {
        return new GlobalConfigExportResult(true, null);
    }

    public static GlobalConfigExportResult Fail(string errorMessage)
    {
        return new GlobalConfigExportResult(false, errorMessage);
    }
}
