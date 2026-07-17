using VSLoader.Models;

namespace VSLoader.Services;

public sealed class AdminUiAutoLoginCoordinator : IDisposable
{
    private readonly Func<AdminUiConfig, string, CancellationToken, Task<AdminUiAutoPasteResult>> runAutoLoginAsync;
    private readonly AdminUiAutoPasteLogService logService;
    private readonly SemaphoreSlim runGate = new(1, 1);
    private readonly object syncRoot = new();
    private CancellationTokenSource? currentCancellationTokenSource;
    private long currentSessionId;
    private bool isShuttingDown;
    private bool disposed;

    public AdminUiAutoLoginCoordinator(AdminUiAutoPasteService autoPasteService, AdminUiAutoPasteLogService logService)
        : this(autoPasteService.TryAutoLoginAsync, logService)
    {
    }

    internal AdminUiAutoLoginCoordinator(
        Func<AdminUiConfig, string, CancellationToken, Task<AdminUiAutoPasteResult>> runAutoLoginAsync,
        AdminUiAutoPasteLogService logService)
    {
        this.runAutoLoginAsync = runAutoLoginAsync;
        this.logService = logService;
    }

    public long Start(
        AdminUiConfig config,
        string password,
        Action<long, AdminUiAutoPasteResult> onCompleted,
        Action<long, Exception> onError)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        CancellationTokenSource cancellationTokenSource;
        long sessionId;
        lock (syncRoot)
        {
            if (isShuttingDown)
            {
                throw new InvalidOperationException("AdminUI 自动登录协调器正在关闭。");
            }

            currentCancellationTokenSource?.Cancel();
            sessionId = ++currentSessionId;
            cancellationTokenSource = new CancellationTokenSource();
            currentCancellationTokenSource = cancellationTokenSource;
            logService.LogTaskStart(sessionId, config, password.Length);
        }

        _ = Task.Run(() => RunAsync(
            sessionId,
            config.Clone(),
            password,
            cancellationTokenSource,
            onCompleted,
            onError));

        return sessionId;
    }

    public bool IsCurrentSession(long sessionId)
    {
        lock (syncRoot)
        {
            return !disposed && !isShuttingDown && sessionId == currentSessionId;
        }
    }

    public void CancelWaitingTask(string reason = "CancelWaitingTask")
    {
        lock (syncRoot)
        {
            var canceledSessionId = currentSessionId;
            currentCancellationTokenSource?.Cancel();
            if (currentCancellationTokenSource is not null)
            {
                logService.LogTaskCancel(canceledSessionId, reason);
            }

            currentSessionId++;
        }
    }

    public void Shutdown()
    {
        lock (syncRoot)
        {
            isShuttingDown = true;
            currentCancellationTokenSource?.Cancel();
            if (currentCancellationTokenSource is not null)
            {
                logService.LogTaskCancel(currentSessionId, "ApplicationShutdown");
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        lock (syncRoot)
        {
            currentCancellationTokenSource?.Cancel();
            currentCancellationTokenSource = null;
        }
    }

    private async Task RunAsync(
        long sessionId,
        AdminUiConfig config,
        string password,
        CancellationTokenSource cancellationTokenSource,
        Action<long, AdminUiAutoPasteResult> onCompleted,
        Action<long, Exception> onError)
    {
        var enteredGate = false;
        try
        {
            await runGate.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            enteredGate = true;
            using (logService.BeginSession(sessionId))
            {
                var result = await runAutoLoginAsync(config, password, cancellationTokenSource.Token).ConfigureAwait(false);
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                logService.LogTaskCompleted(sessionId, result.Status, result.Message);
                TryInvoke(() => onCompleted(sessionId, result));
            }
        }
        catch (OperationCanceledException)
        {
            logService.LogTaskCancel(sessionId, "Canceled");
        }
        catch (Exception ex)
        {
            logService.LogError(ex);
            TryInvoke(() => onError(sessionId, ex));
        }
        finally
        {
            if (enteredGate)
            {
                runGate.Release();
            }

            lock (syncRoot)
            {
                if (ReferenceEquals(currentCancellationTokenSource, cancellationTokenSource))
                {
                    currentCancellationTokenSource = null;
                }
            }

            cancellationTokenSource.Dispose();
        }
    }

    private void TryInvoke(Action callback)
    {
        try
        {
            callback();
        }
        catch (Exception ex)
        {
            logService.LogError(ex);
        }
    }
}
