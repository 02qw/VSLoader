namespace VSLoader.Services;

public sealed class ClipboardService
{
    private readonly Action<string> setText;

    public ClipboardService()
        : this(text => System.Windows.Clipboard.SetDataObject(text, true))
    {
    }

    internal ClipboardService(Action<string> setText)
    {
        this.setText = setText;
    }

    public async Task<SaveResult> SetTextWithRetryAsync(
        string text,
        int maxAttempts = 15,
        int delayMilliseconds = 120,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SaveResult.Fail("剪贴板内容为空。");
        }

        Exception? lastException = null;
        var attemptCount = Math.Max(1, maxAttempts);
        var delay = Math.Max(0, delayMilliseconds);

        for (var attempt = 1; attempt <= attemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                setText(text);
                return SaveResult.Ok();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < attemptCount && delay > 0)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        return SaveResult.Fail(BuildFailureMessage(attemptCount, lastException));
    }

    private static string BuildFailureMessage(int attemptCount, Exception? exception)
    {
        if (exception is null)
        {
            return $"写入剪贴板失败，已重试 {attemptCount} 次。未知错误";
        }

        return $"写入剪贴板失败，已重试 {attemptCount} 次。HResult=0x{exception.HResult:X8}, Message={exception.Message}";
    }
}
