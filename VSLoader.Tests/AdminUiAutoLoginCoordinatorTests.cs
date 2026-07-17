using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class AdminUiAutoLoginCoordinatorTests
{
    [Fact]
    public async Task Start_cancels_previous_waiting_task_and_runs_latest_task()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        var completed = new List<AdminUiAutoPasteResult>();
        using var coordinator = new AdminUiAutoLoginCoordinator(
            async (_, _, token) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    firstStarted.SetResult();
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), token);
                    }
                    catch (OperationCanceledException)
                    {
                        firstCancelled.SetResult();
                        throw;
                    }
                }

                secondStarted.SetResult();
                return AdminUiAutoPasteResult.TimedOut();
            },
            CreateLogService());

        coordinator.Start(new AdminUiConfig(), "first", (_, result) => completed.Add(result), (_, ex) => throw ex);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var latestSessionId = coordinator.Start(new AdminUiConfig(), "second", (_, result) => completed.Add(result), (_, ex) => throw ex);

        await firstCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(100);

        Assert.Single(completed);
        Assert.Equal(AdminUiAutoLoginStatus.TimedOut, completed[0].Status);
        Assert.True(coordinator.IsCurrentSession(latestSessionId));
    }

    [Fact]
    public async Task Start_passes_password_to_background_runner()
    {
        string? observedPassword = null;
        using var coordinator = new AdminUiAutoLoginCoordinator(
            (_, password, _) =>
            {
                observedPassword = password;
                return Task.FromResult(AdminUiAutoPasteResult.TimedOut());
            },
            CreateLogService());
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        long observedSessionId = 0;
        var sessionId = coordinator.Start(
            new AdminUiConfig(),
            "A1!",
            (completedSessionId, _) =>
            {
                observedSessionId = completedSessionId;
                completed.SetResult();
            },
            (_, ex) => completed.SetException(ex));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("A1!", observedPassword);
        Assert.Equal(sessionId, observedSessionId);
        Assert.True(coordinator.IsCurrentSession(sessionId));
    }

    [Fact]
    public void Shutdown_invalidates_current_session()
    {
        using var coordinator = new AdminUiAutoLoginCoordinator(
            (_, _, token) => Task.FromCanceled<AdminUiAutoPasteResult>(token),
            CreateLogService());

        var sessionId = coordinator.Start(new AdminUiConfig(), "A1!", (_, _) => { }, (_, _) => { });
        coordinator.Shutdown();

        Assert.False(coordinator.IsCurrentSession(sessionId));
    }

    [Fact]
    public void Coordinator_explicitly_schedules_work_off_the_ui_context()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Models",
            "Services",
            "AdminUiAutoLoginCoordinator.cs"));

        Assert.Contains("_ = Task.Run", code);
        Assert.Contains("ConfigureAwait(false)", code);
    }

    private static AdminUiAutoPasteLogService CreateLogService()
    {
        return new AdminUiAutoPasteLogService(Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N")));
    }
}
