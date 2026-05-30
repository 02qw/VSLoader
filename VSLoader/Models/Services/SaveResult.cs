namespace VSLoader.Services;

public sealed class SaveResult
{
    private SaveResult(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public static SaveResult Ok()
    {
        return new SaveResult(true, null);
    }

    public static SaveResult Fail(string errorMessage)
    {
        return new SaveResult(false, errorMessage);
    }
}
