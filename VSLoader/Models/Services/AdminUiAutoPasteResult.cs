namespace VSLoader.Services;

public sealed class AdminUiAutoPasteResult
{
    private AdminUiAutoPasteResult(bool success, string message, ForegroundWindowInfo? matchedWindow)
    {
        Success = success;
        Message = message;
        MatchedWindow = matchedWindow;
    }

    public bool Success { get; }

    public string Message { get; }

    public ForegroundWindowInfo? MatchedWindow { get; }

    public static AdminUiAutoPasteResult Ok(ForegroundWindowInfo matchedWindow)
    {
        return new AdminUiAutoPasteResult(true, "已自动粘贴密码并回车。", matchedWindow);
    }

    public static AdminUiAutoPasteResult Fail(string message)
    {
        return new AdminUiAutoPasteResult(false, message, null);
    }
}
