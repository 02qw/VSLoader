using System.Diagnostics;
using System.IO;
using System.Text;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class PowerShellCapabilityExecutor
{
    private const int MaximumCapturedCharacters = 64 * 1024;
    private readonly Func<string?> resolvePowerShellPath;

    public PowerShellCapabilityExecutor()
        : this(ResolvePowerShellPath)
    {
    }

    internal PowerShellCapabilityExecutor(Func<string?> resolvePowerShellPath)
    {
        this.resolvePowerShellPath = resolvePowerShellPath;
    }

    internal static string EncodeScript(string script)
    {
        return Convert.ToBase64String(Encoding.Unicode.GetBytes(script ?? string.Empty));
    }

    public async Task<ContextMenuCapabilityExecutionResult> ExecuteAsync(
        ContextMenuCapabilityDefinition definition,
        ContextMenuCapabilityExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var powershellPath = resolvePowerShellPath();
            if (string.IsNullOrWhiteSpace(powershellPath) || !File.Exists(powershellPath))
            {
                return ContextMenuCapabilityExecutionResult.Fail("未找到 Windows PowerShell，无法执行命令能力。");
            }

            var config = definition.PowerShell ?? new PowerShellCapabilityConfig();
            var workingDirectoryResult = ResolveWorkingDirectory(config.WorkingDirectoryMode, context);
            if (!workingDirectoryResult.Success)
            {
                return ContextMenuCapabilityExecutionResult.Fail(workingDirectoryResult.ErrorMessage);
            }

            var background = string.Equals(
                config.ExecutionMode,
                PowerShellCapabilityExecutionModes.Background,
                StringComparison.Ordinal);
            var startInfo = new ProcessStartInfo
            {
                FileName = powershellPath,
                WorkingDirectory = workingDirectoryResult.Path,
                UseShellExecute = false,
                CreateNoWindow = background,
                RedirectStandardOutput = background,
                RedirectStandardError = background
            };
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-EncodedCommand");
            startInfo.ArgumentList.Add(EncodeScript(config.Script));

            foreach (var pair in ContextMenuCapabilityVariableService.BuildEnvironmentVariables(context))
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!background)
            {
                return process.Start()
                    ? ContextMenuCapabilityExecutionResult.Ok("PowerShell 命令已启动。", started: true)
                    : ContextMenuCapabilityExecutionResult.Fail("PowerShell 进程未能启动。");
            }

            var standardOutput = new BoundedProcessOutput(MaximumCapturedCharacters);
            var standardError = new BoundedProcessOutput(MaximumCapturedCharacters);
            process.OutputDataReceived += (_, eventArgs) => standardOutput.AppendLine(eventArgs.Data);
            process.ErrorDataReceived += (_, eventArgs) => standardError.AppendLine(eventArgs.Data);
            if (!process.Start())
            {
                return ContextMenuCapabilityExecutionResult.Fail("PowerShell 进程未能启动。");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var waitTask = process.WaitForExitAsync(CancellationToken.None);
            var timeout = TimeSpan.FromSeconds(Math.Clamp(config.TimeoutSeconds, 1, 300));
            var delayTask = Task.Delay(timeout, cancellationToken);
            var completed = await Task.WhenAny(waitTask, delayTask);
            if (completed != waitTask)
            {
                TryKillProcessTree(process);
                if (cancellationToken.IsCancellationRequested)
                {
                    return ContextMenuCapabilityExecutionResult.Cancel("PowerShell 命令已取消。");
                }

                return ContextMenuCapabilityExecutionResult.Fail(
                    $"PowerShell 命令执行超时（{config.TimeoutSeconds} 秒）。",
                    standardOutput: standardOutput.GetText(),
                    standardError: standardError.GetText(),
                    started: true,
                    timedOut: true,
                    outputTruncated: standardOutput.Truncated || standardError.Truncated);
            }

            await waitTask;
            var output = standardOutput.GetText();
            var error = standardError.GetText();
            var truncated = standardOutput.Truncated || standardError.Truncated;
            return process.ExitCode == 0
                ? ContextMenuCapabilityExecutionResult.Ok(
                    "PowerShell 命令执行完成。",
                    process.ExitCode,
                    output,
                    error,
                    started: true,
                    outputTruncated: truncated)
                : ContextMenuCapabilityExecutionResult.Fail(
                    $"PowerShell 命令执行失败，退出码：{process.ExitCode}。",
                    process.ExitCode,
                    output,
                    error,
                    started: true,
                    outputTruncated: truncated);
        }
        catch (OperationCanceledException)
        {
            return ContextMenuCapabilityExecutionResult.Cancel("PowerShell 命令已取消。");
        }
        catch (Exception ex)
        {
            return ContextMenuCapabilityExecutionResult.Fail($"PowerShell 命令启动失败：{ex.Message}");
        }
    }

    private static WorkingDirectoryResult ResolveWorkingDirectory(
        string mode,
        ContextMenuCapabilityExecutionContext context)
    {
        var targetPath = context.Shortcut?.TargetPath?.Trim() ?? string.Empty;
        string path;
        if (string.Equals(mode, PowerShellCapabilityWorkingDirectoryModes.Target, StringComparison.Ordinal))
        {
            path = Directory.Exists(targetPath)
                ? targetPath
                : ContextMenuCapabilityVariableService.GetTargetParent(targetPath);
        }
        else if (string.Equals(mode, PowerShellCapabilityWorkingDirectoryModes.TargetParent, StringComparison.Ordinal))
        {
            path = ContextMenuCapabilityVariableService.GetTargetParent(targetPath);
        }
        else if (string.Equals(mode, PowerShellCapabilityWorkingDirectoryModes.Workspace, StringComparison.Ordinal))
        {
            path = context.WorkspaceDirectory?.Trim() ?? string.Empty;
        }
        else if (string.Equals(mode, PowerShellCapabilityWorkingDirectoryModes.App, StringComparison.Ordinal))
        {
            path = context.AppBaseDirectory?.Trim() ?? string.Empty;
        }
        else
        {
            return WorkingDirectoryResult.Fail($"PowerShell 工作目录模式无效：{mode}。");
        }

        return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
            ? WorkingDirectoryResult.Ok(path)
            : WorkingDirectoryResult.Fail($"PowerShell 工作目录不存在：{path}。");
    }

    private static string? ResolvePowerShellPath()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var systemPowerShell = Path.Combine(
            systemRoot,
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        if (File.Exists(systemPowerShell))
        {
            return systemPowerShell;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), "powershell.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore malformed PATH entries and continue resolving.
            }
        }

        return null;
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Process termination is best effort; execution errors are returned to the caller.
        }
    }

    private sealed class BoundedProcessOutput
    {
        private readonly int maximumCharacters;
        private readonly StringBuilder builder = new();
        private readonly object syncRoot = new();

        public BoundedProcessOutput(int maximumCharacters)
        {
            this.maximumCharacters = maximumCharacters;
        }

        public bool Truncated { get; private set; }

        public void AppendLine(string? value)
        {
            if (value is null)
            {
                return;
            }

            lock (syncRoot)
            {
                var remaining = maximumCharacters - builder.Length;
                if (remaining <= 0)
                {
                    Truncated = true;
                    return;
                }

                var line = value + Environment.NewLine;
                if (line.Length > remaining)
                {
                    builder.Append(line.AsSpan(0, remaining));
                    Truncated = true;
                    return;
                }

                builder.Append(line);
            }
        }

        public string GetText()
        {
            lock (syncRoot)
            {
                return builder.ToString();
            }
        }
    }

    private sealed record WorkingDirectoryResult(bool Success, string Path, string ErrorMessage)
    {
        public static WorkingDirectoryResult Ok(string path) => new(true, path, string.Empty);

        public static WorkingDirectoryResult Fail(string errorMessage) => new(false, string.Empty, errorMessage);
    }
}
