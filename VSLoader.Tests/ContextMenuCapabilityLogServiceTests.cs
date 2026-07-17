using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ContextMenuCapabilityLogServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Log_keeps_only_latest_2000_lines_and_does_not_write_script_body()
    {
        const int testLimit = 5;
        var service = new ContextMenuCapabilityLogService(root, testLimit);
        var definition = new ContextMenuCapabilityDefinition
        {
            Id = "secret-command",
            Name = "命令",
            Kind = ContextMenuCapabilityKinds.PowerShell,
            PowerShell = new PowerShellCapabilityConfig { Script = "TOP-SECRET-SCRIPT" }
        };
        var context = new ContextMenuCapabilityExecutionContext
        {
            Shortcut = new ShortcutItem { Name = "设备", TargetPath = @"C:\Device" },
            Surface = ContextMenuCapabilitySurfaces.ShortcutList
        };

        for (var index = 0; index < 8; index++)
        {
            service.Log(definition, context, "Completed", ContextMenuCapabilityExecutionResult.Ok($"run-{index}"), TimeSpan.Zero);
        }

        var lines = File.ReadAllLines(Path.Combine(root, "context-menu-capability.log"));
        Assert.Equal(2000, ContextMenuCapabilityLogService.MaximumLogLines);
        Assert.Equal(testLimit, lines.Length);
        Assert.DoesNotContain(lines, line => line.Contains("run-0", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("run-7", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("TOP-SECRET-SCRIPT", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
