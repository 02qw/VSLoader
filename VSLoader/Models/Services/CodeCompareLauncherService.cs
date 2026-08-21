using System.Diagnostics;
using System.IO;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class CodeCompareLauncherService
{
    public const string ExecutableRelativePath = @"Tools\CodeCompare\UpAndown.CodeCompare.exe";

    public ContextMenuCapabilityExecutionResult Launch(
        string appBaseDirectory,
        string localModulePath,
        string remoteModulePath,
        CodeCompareConfig config,
        string vscodePath)
    {
        var executablePath = Path.Combine(appBaseDirectory, ExecutableRelativePath);
        if (!File.Exists(executablePath))
        {
            return ContextMenuCapabilityExecutionResult.Fail(
                $"没有找到代码对比工具：{executablePath}。请重新安装或更新 VSLoader。", started: false);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? appBaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--local");
            startInfo.ArgumentList.Add(localModulePath);
            startInfo.ArgumentList.Add("--remote");
            startInfo.ArgumentList.Add(remoteModulePath);
            startInfo.ArgumentList.Add("--scope");
            startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(config.DefaultScanScope)
                ? @"config\deo"
                : config.DefaultScanScope.Trim());
            startInfo.ArgumentList.Add("--ide");
            startInfo.ArgumentList.Add("vscode");
            startInfo.ArgumentList.Add("--vscode-path");
            startInfo.ArgumentList.Add(vscodePath?.Trim() ?? string.Empty);
            startInfo.ArgumentList.Add("--auto-scan");
            startInfo.ArgumentList.Add(config.AutoScan ? "true" : "false");

            Process.Start(startInfo);
            return ContextMenuCapabilityExecutionResult.Ok("代码对比工具已打开。", started: true);
        }
        catch (Exception ex)
        {
            return ContextMenuCapabilityExecutionResult.Fail($"启动代码对比工具失败：{ex.Message}");
        }
    }
}
