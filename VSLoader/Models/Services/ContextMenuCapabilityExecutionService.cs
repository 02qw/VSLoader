using System.Collections.Concurrent;
using System.Diagnostics;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class ContextMenuCapabilityExecutionService
{
    private readonly ContextMenuCapabilityConfigService configService;
    private readonly ContextMenuCapabilityTrustService trustService;
    private readonly ContextMenuCapabilityLogService logService;
    private readonly DialogService dialogService;
    private readonly PathAccessPreflightService pathAccessPreflightService;
    private readonly Func<ContextMenuCapabilityDefinition, ContextMenuCapabilityExecutionContext, CancellationToken, Task<ContextMenuCapabilityExecutionResult>> executePowerShellAsync;
    private readonly Func<ContextMenuCapabilityDefinition, ContextMenuCapabilityExecutionContext, CancellationToken, Task<ContextMenuCapabilityExecutionResult>> executeWebAsync;
    private readonly Func<string, ContextMenuCapabilityExecutionContext, CancellationToken, Task<ContextMenuCapabilityExecutionResult>> executeBuiltInAsync;
    private readonly ConcurrentDictionary<string, byte> runningOperations = new(StringComparer.OrdinalIgnoreCase);

    public ContextMenuCapabilityExecutionService(
        DialogService dialogService,
        Func<string, ContextMenuCapabilityExecutionContext, CancellationToken, Task<ContextMenuCapabilityExecutionResult>> executeBuiltInAsync,
        ContextMenuCapabilityTrustService? trustService = null)
        : this(
            new ContextMenuCapabilityConfigService(),
            trustService ?? new ContextMenuCapabilityTrustService(),
            new ContextMenuCapabilityLogService(),
            dialogService,
            new PowerShellCapabilityExecutor().ExecuteAsync,
            new WebCapabilityExecutor().ExecuteAsync,
            executeBuiltInAsync,
            new PathAccessPreflightService())
    {
    }

    internal ContextMenuCapabilityExecutionService(
        ContextMenuCapabilityConfigService configService,
        ContextMenuCapabilityTrustService trustService,
        ContextMenuCapabilityLogService logService,
        DialogService dialogService,
        Func<ContextMenuCapabilityDefinition, ContextMenuCapabilityExecutionContext, CancellationToken, Task<ContextMenuCapabilityExecutionResult>> executePowerShellAsync,
        Func<ContextMenuCapabilityDefinition, ContextMenuCapabilityExecutionContext, CancellationToken, Task<ContextMenuCapabilityExecutionResult>> executeWebAsync,
        Func<string, ContextMenuCapabilityExecutionContext, CancellationToken, Task<ContextMenuCapabilityExecutionResult>> executeBuiltInAsync,
        PathAccessPreflightService? pathAccessPreflightService = null)
    {
        this.configService = configService;
        this.trustService = trustService;
        this.logService = logService;
        this.dialogService = dialogService;
        this.executePowerShellAsync = executePowerShellAsync;
        this.executeWebAsync = executeWebAsync;
        this.executeBuiltInAsync = executeBuiltInAsync;
        this.pathAccessPreflightService = pathAccessPreflightService ?? new PathAccessPreflightService();
    }

    public async Task<ContextMenuCapabilityExecutionResult> ExecuteAsync(
        ContextMenuCapabilityDefinition definition,
        ContextMenuCapabilityExecutionContext context,
        CancellationToken cancellationToken)
    {
        var validation = configService.Validate(definition);
        if (!validation.Success)
        {
            return ContextMenuCapabilityExecutionResult.Fail(validation.ErrorMessage ?? "能力配置无效。");
        }

        if (!definition.Enabled)
        {
            return ContextMenuCapabilityExecutionResult.Fail($"能力“{definition.Name}”已停用。");
        }

        if (!IsAllowedOnSurface(definition, context.Surface))
        {
            return ContextMenuCapabilityExecutionResult.Fail($"能力“{definition.Name}”不允许在当前界面执行。");
        }

        if (definition.RequiresExistingTargetPath)
        {
            var preflight = await pathAccessPreflightService.CheckDirectoryAsync(context.Shortcut?.TargetPath ?? string.Empty);
            if (!preflight.Success)
            {
                return ContextMenuCapabilityExecutionResult.Fail(preflight.ErrorMessage ?? "目标路径不存在或不可访问。");
            }
        }

        if (string.Equals(definition.Kind, ContextMenuCapabilityKinds.PowerShell, StringComparison.Ordinal)
            && !trustService.IsTrusted(definition))
        {
            var trustMessage = "PowerShell 命令可以读取、修改或删除当前用户有权限访问的文件，并可启动其他程序。"
                + "请只执行你理解并信任的命令。\n\n"
                + $"能力：{definition.Name}\n"
                + $"脚本：\n{definition.PowerShell.Script}\n\n"
                + "是否信任并继续执行？";
            if (!dialogService.Confirm(trustMessage))
            {
                return ContextMenuCapabilityExecutionResult.Cancel("用户取消了 PowerShell 能力信任确认。");
            }

            var trustResult = trustService.Trust(definition);
            if (!trustResult.Success)
            {
                return ContextMenuCapabilityExecutionResult.Fail(trustResult.ErrorMessage ?? "保存命令信任状态失败。");
            }
        }

        if (definition.ConfirmBeforeExecute
            && !dialogService.Confirm($"确定执行“{definition.Name}”吗？\n\n目标：{context.Shortcut?.Name}\n路径：{context.Shortcut?.TargetPath}"))
        {
            return ContextMenuCapabilityExecutionResult.Cancel();
        }

        var operationKey = BuildOperationKey(definition, context);
        if (!runningOperations.TryAdd(operationKey, 0))
        {
            return ContextMenuCapabilityExecutionResult.Fail($"能力“{definition.Name}”正在执行该快捷项，请等待完成。");
        }

        var stopwatch = Stopwatch.StartNew();
        ContextMenuCapabilityExecutionResult result;
        try
        {
            result = definition.Kind switch
            {
                ContextMenuCapabilityKinds.BuiltIn => await executeBuiltInAsync(
                    definition.BuiltInActionId,
                    context,
                    cancellationToken),
                ContextMenuCapabilityKinds.PowerShell => await executePowerShellAsync(definition, context, cancellationToken),
                ContextMenuCapabilityKinds.Web => await executeWebAsync(definition, context, cancellationToken),
                _ => ContextMenuCapabilityExecutionResult.Fail($"能力类型不受支持：{definition.Kind}。")
            };
        }
        catch (OperationCanceledException)
        {
            result = ContextMenuCapabilityExecutionResult.Cancel();
        }
        catch (Exception ex)
        {
            result = ContextMenuCapabilityExecutionResult.Fail($"能力执行失败：{ex.Message}");
        }
        finally
        {
            runningOperations.TryRemove(operationKey, out _);
        }

        stopwatch.Stop();
        logService.Log(definition, context, "Completed", result, stopwatch.Elapsed);
        return result;
    }

    public static string BuildFailureDetails(
        ContextMenuCapabilityDefinition definition,
        ContextMenuCapabilityExecutionContext context,
        ContextMenuCapabilityExecutionResult result)
    {
        var lines = new List<string>
        {
            $"能力名称：{definition.Name}",
            $"能力类型：{definition.Kind}",
            $"快捷项：{context.Shortcut?.Name}",
            $"目标路径：{context.Shortcut?.TargetPath}",
            $"失败原因：{result.Message}"
        };
        if (result.ExitCode is not null)
        {
            lines.Add($"退出码：{result.ExitCode}");
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            lines.Add($"错误输出：\n{result.StandardError.Trim()}");
        }

        if (result.OutputTruncated)
        {
            lines.Add("输出过长，已截断。");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsAllowedOnSurface(ContextMenuCapabilityDefinition definition, string surface)
    {
        return string.Equals(surface, ContextMenuCapabilitySurfaces.ShortcutList, StringComparison.Ordinal)
            ? definition.ShowInShortcutList
            : string.Equals(surface, ContextMenuCapabilitySurfaces.FactoryMap, StringComparison.Ordinal)
                && definition.ShowInFactoryMap;
    }

    private static string BuildOperationKey(
        ContextMenuCapabilityDefinition definition,
        ContextMenuCapabilityExecutionContext context)
    {
        return $"{definition.Id}\n{context.Shortcut?.TargetPath}";
    }
}
