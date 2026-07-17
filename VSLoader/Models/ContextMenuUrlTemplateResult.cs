namespace VSLoader.Models;

public sealed class ContextMenuUrlTemplateResult
{
    private ContextMenuUrlTemplateResult(bool success, string url, string errorMessage)
    {
        Success = success;
        Url = url;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public string Url { get; }

    public string ErrorMessage { get; }

    public static ContextMenuUrlTemplateResult Ok(string url) => new(true, url, string.Empty);

    public static ContextMenuUrlTemplateResult Fail(string errorMessage) => new(false, string.Empty, errorMessage);
}
