using System.Runtime.InteropServices;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ClipboardServiceTests
{
    [Fact]
    public async Task SetTextWithRetryAsync_retries_transient_clipboard_open_failures()
    {
        var attempts = 0;
        var service = new ClipboardService(_ =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new COMException("OpenClipboard Failed", unchecked((int)0x800401D0));
            }
        });

        var result = await service.SetTextWithRetryAsync("secret", maxAttempts: 5, delayMilliseconds: 0);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task SetTextWithRetryAsync_reports_attempt_count_hresult_and_message_after_failure()
    {
        var attempts = 0;
        var service = new ClipboardService(_ =>
        {
            attempts++;
            throw new COMException("OpenClipboard Failed", unchecked((int)0x800401D0));
        });

        var result = await service.SetTextWithRetryAsync("secret", maxAttempts: 4, delayMilliseconds: 0);

        Assert.False(result.Success);
        Assert.Equal(4, attempts);
        Assert.Contains("已重试 4 次", result.ErrorMessage);
        Assert.Contains("0x800401D0", result.ErrorMessage);
        Assert.Contains("OpenClipboard Failed", result.ErrorMessage);
    }

    [Fact]
    public async Task SetTextWithRetryAsync_stops_retrying_when_canceled()
    {
        var attempts = 0;
        var service = new ClipboardService(_ =>
        {
            attempts++;
            throw new COMException("OpenClipboard Failed", unchecked((int)0x800401D0));
        });
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(30);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SetTextWithRetryAsync(
            "secret",
            maxAttempts: 15,
            delayMilliseconds: 120,
            cancellationTokenSource.Token));

        Assert.Equal(1, attempts);
    }
}
