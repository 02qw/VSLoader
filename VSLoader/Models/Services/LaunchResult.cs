namespace VSLoader.Services;

public sealed class LaunchResult
{
    private LaunchResult(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public static LaunchResult Ok()
    {
        return new LaunchResult(true, null);
    }

    public static LaunchResult Fail(string errorMessage)
    {
        return new LaunchResult(false, errorMessage);
    }
}
