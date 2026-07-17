using System.Text;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class PowerShellCapabilityExecutorTests
{
    [Fact]
    public void EncodeScript_round_trips_multiline_chinese_script()
    {
        const string script = "Write-Output '设备 A'\r\nWrite-Output $env:VSL_TARGET_PATH";

        var encoded = PowerShellCapabilityExecutor.EncodeScript(script);
        var decoded = Encoding.Unicode.GetString(Convert.FromBase64String(encoded));

        Assert.Equal(script, decoded);
    }

    [Fact]
    public async Task ExecuteAsync_background_mode_receives_context_environment_variables()
    {
        var executor = new PowerShellCapabilityExecutor();
        var definition = CreateBackgroundDefinition("Write-Output $env:VSL_TARGET_PATH");
        var context = new ContextMenuCapabilityExecutionContext
        {
            Shortcut = new ShortcutItem { Name = "设备A", TargetPath = @"C:\Line 1\设备A" },
            WorkspaceId = "default",
            WorkspaceDirectory = Path.GetTempPath(),
            AppBaseDirectory = AppContext.BaseDirectory,
            Surface = ContextMenuCapabilitySurfaces.ShortcutList
        };

        var result = await executor.ExecuteAsync(definition, context, CancellationToken.None);

        Assert.True(result.Success, result.Message + Environment.NewLine + result.StandardError);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(@"C:\Line 1\设备A", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_background_mode_receives_target_identity_environment_variables()
    {
        var executor = new PowerShellCapabilityExecutor();
        var definition = CreateBackgroundDefinition(
            "Write-Output \"$env:VSL_TARGET_NAME|$env:VSL_INSTANCE_ID|$env:VSL_DEVICE_CODE|$env:VSL_DEVICE_TYPE|$env:VSL_DEVICE_NUMBER\"");
        var context = CreateContext();
        context.Shortcut.TargetPath = @"C:\instances\5924_TSSP002";

        var result = await executor.ExecuteAsync(definition, context, CancellationToken.None);

        Assert.True(result.Success, result.Message + Environment.NewLine + result.StandardError);
        Assert.Contains("5924_TSSP002|5924|TSSP002|TSSP|002", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_reports_nonzero_exit_code_and_stderr()
    {
        var executor = new PowerShellCapabilityExecutor();
        var definition = CreateBackgroundDefinition("[Console]::Error.WriteLine('failed'); exit 7");

        var result = await executor.ExecuteAsync(
            definition,
            CreateContext(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(7, result.ExitCode);
        Assert.Contains("failed", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_times_out_and_terminates_background_process()
    {
        var executor = new PowerShellCapabilityExecutor();
        var definition = CreateBackgroundDefinition("Start-Sleep -Seconds 5");
        definition.PowerShell.TimeoutSeconds = 1;

        var result = await executor.ExecuteAsync(definition, CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.TimedOut);
        Assert.Contains("超时", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_returns_traceable_error_when_powershell_is_missing()
    {
        var executor = new PowerShellCapabilityExecutor(() => null);

        var result = await executor.ExecuteAsync(
            CreateBackgroundDefinition("Write-Output 1"),
            CreateContext(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("未找到 Windows PowerShell", result.Message, StringComparison.Ordinal);
    }

    private static ContextMenuCapabilityDefinition CreateBackgroundDefinition(string script)
    {
        return new ContextMenuCapabilityDefinition
        {
            Id = "ps-test",
            Name = "测试命令",
            Kind = ContextMenuCapabilityKinds.PowerShell,
            RequiresExistingTargetPath = false,
            PowerShell = new PowerShellCapabilityConfig
            {
                Script = script,
                ExecutionMode = PowerShellCapabilityExecutionModes.Background,
                WorkingDirectoryMode = PowerShellCapabilityWorkingDirectoryModes.Workspace,
                TimeoutSeconds = 10
            }
        };
    }

    private static ContextMenuCapabilityExecutionContext CreateContext()
    {
        return new ContextMenuCapabilityExecutionContext
        {
            Shortcut = new ShortcutItem { Name = "测试", TargetPath = Path.GetTempPath() },
            WorkspaceDirectory = Path.GetTempPath(),
            AppBaseDirectory = AppContext.BaseDirectory,
            Surface = ContextMenuCapabilitySurfaces.ShortcutList
        };
    }
}
