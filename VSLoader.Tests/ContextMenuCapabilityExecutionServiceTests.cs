using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ContextMenuCapabilityExecutionServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExecuteAsync_requires_local_trust_then_separate_execution_confirmation()
    {
        var definition = CreatePowerShellDefinition();
        definition.ConfirmBeforeExecute = true;
        var dialog = new RecordingDialogService(true, true);
        var invoked = 0;
        var service = CreateService(
            dialog,
            (_, _, _) =>
            {
                invoked++;
                return Task.FromResult(ContextMenuCapabilityExecutionResult.Ok());
            });

        var result = await service.ExecuteAsync(definition, CreateContext(), CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, invoked);
        Assert.Equal(2, dialog.ConfirmMessages.Count);
        Assert.Contains("PowerShell 命令可以", dialog.ConfirmMessages[0], StringComparison.Ordinal);
        Assert.Contains("确定执行", dialog.ConfirmMessages[1], StringComparison.Ordinal);
        Assert.True(new ContextMenuCapabilityTrustService(Path.Combine(root, "trust.json")).IsTrusted(definition));
    }

    [Fact]
    public async Task ExecuteAsync_cancels_without_starting_when_trust_is_rejected()
    {
        var invoked = 0;
        var service = CreateService(
            new RecordingDialogService(false),
            (_, _, _) =>
            {
                invoked++;
                return Task.FromResult(ContextMenuCapabilityExecutionResult.Ok());
            });

        var result = await service.ExecuteAsync(CreatePowerShellDefinition(), CreateContext(), CancellationToken.None);

        Assert.True(result.Cancelled);
        Assert.Equal(0, invoked);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_duplicate_background_execution_for_same_action_and_target()
    {
        var definition = CreatePowerShellDefinition();
        var trust = new ContextMenuCapabilityTrustService(Path.Combine(root, "trust.json"));
        Assert.True(trust.Trust(definition).Success);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateService(
            new RecordingDialogService(),
            async (_, _, _) =>
            {
                started.SetResult();
                await release.Task;
                return ContextMenuCapabilityExecutionResult.Ok();
            });

        var first = service.ExecuteAsync(definition, CreateContext(), CancellationToken.None);
        await started.Task;
        var second = await service.ExecuteAsync(definition, CreateContext(), CancellationToken.None);
        release.SetResult();
        await first;

        Assert.False(second.Success);
        Assert.Contains("正在执行", second.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_dispatches_builtin_with_explicit_shortcut_context()
    {
        ContextMenuCapabilityExecutionContext? received = null;
        var service = CreateService(
            new RecordingDialogService(),
            (_, _, _) => Task.FromResult(ContextMenuCapabilityExecutionResult.Ok()),
            (_, context, _) =>
            {
                received = context;
                return Task.FromResult(ContextMenuCapabilityExecutionResult.Ok());
            });
        var definition = ContextMenuCapabilityDefaults.CreateBuiltIn(ContextMenuBuiltInActionIds.OpenVsCode, 0);
        var context = CreateContext();

        var result = await service.ExecuteAsync(definition, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Same(context.Shortcut, received!.Shortcut);
        Assert.Equal(ContextMenuCapabilitySurfaces.FactoryMap, received.Surface);
    }

    private ContextMenuCapabilityExecutionService CreateService(
        DialogService dialog,
        Func<ContextMenuCapabilityDefinition, ContextMenuCapabilityExecutionContext, CancellationToken, Task<ContextMenuCapabilityExecutionResult>> powerShell,
        Func<string, ContextMenuCapabilityExecutionContext, CancellationToken, Task<ContextMenuCapabilityExecutionResult>>? builtIn = null)
    {
        return new ContextMenuCapabilityExecutionService(
            new ContextMenuCapabilityConfigService(),
            new ContextMenuCapabilityTrustService(Path.Combine(root, "trust.json")),
            new ContextMenuCapabilityLogService(Path.Combine(root, "logs")),
            dialog,
            powerShell,
            (_, _, _) => Task.FromResult(ContextMenuCapabilityExecutionResult.Ok()),
            builtIn ?? ((_, _, _) => Task.FromResult(ContextMenuCapabilityExecutionResult.Ok())));
    }

    private static ContextMenuCapabilityDefinition CreatePowerShellDefinition()
    {
        return new ContextMenuCapabilityDefinition
        {
            Id = "custom-command",
            Name = "测试命令",
            Kind = ContextMenuCapabilityKinds.PowerShell,
            RequiresExistingTargetPath = false,
            PowerShell = new PowerShellCapabilityConfig
            {
                Script = "Write-Output 1",
                ExecutionMode = PowerShellCapabilityExecutionModes.Background,
                WorkingDirectoryMode = PowerShellCapabilityWorkingDirectoryModes.Workspace
            }
        };
    }

    private static ContextMenuCapabilityExecutionContext CreateContext()
    {
        return new ContextMenuCapabilityExecutionContext
        {
            Shortcut = new ShortcutItem { Name = "地图节点", TargetPath = Path.GetTempPath() },
            WorkspaceId = "default",
            WorkspaceDirectory = Path.GetTempPath(),
            AppBaseDirectory = AppContext.BaseDirectory,
            Surface = ContextMenuCapabilitySurfaces.FactoryMap
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class RecordingDialogService(params bool[] confirmResults) : DialogService
    {
        private readonly Queue<bool> results = new(confirmResults);

        public List<string> ConfirmMessages { get; } = [];

        public override bool Confirm(string message)
        {
            ConfirmMessages.Add(message);
            return results.Count == 0 || results.Dequeue();
        }
    }
}
