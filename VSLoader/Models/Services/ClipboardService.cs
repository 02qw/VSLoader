namespace VSLoader.Services;

public sealed class ClipboardService
{
    public async Task<SaveResult> SetTextWithRetryAsync(
        string text,
        int maxAttempts = 5,
        int delayMilliseconds = 120)
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
            try
            {
                System.Windows.Clipboard.SetDataObject(text, true);
                return SaveResult.Ok();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < attemptCount && delay > 0)
                {
                    await Task.Delay(delay);
                }
            }
        }

        return SaveResult.Fail(lastException?.Message ?? "未知错误");
    }
}
