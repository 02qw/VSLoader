using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ContextMenuCapabilityTrustServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Trust_is_invalidated_when_script_changes()
    {
        var service = new ContextMenuCapabilityTrustService(Path.Combine(root, "trust.json"));
        var definition = CreateDefinition("Write-Output 1");

        service.Trust(definition);

        Assert.True(service.IsTrusted(definition));
        definition.PowerShell.Script = "Write-Output 2";
        Assert.False(service.IsTrusted(definition));
    }

    [Fact]
    public void Corrupted_trust_file_is_treated_as_untrusted()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "trust.json");
        File.WriteAllText(path, "{ broken");
        var service = new ContextMenuCapabilityTrustService(path);

        Assert.False(service.IsTrusted(CreateDefinition("Write-Output 1")));
    }

    [Fact]
    public void ComputeHash_changes_when_security_related_execution_options_change()
    {
        var definition = CreateDefinition("Write-Output 1");
        var original = ContextMenuCapabilityTrustService.ComputeHash(definition);

        definition.PowerShell.ExecutionMode = PowerShellCapabilityExecutionModes.Visible;

        Assert.NotEqual(original, ContextMenuCapabilityTrustService.ComputeHash(definition));
    }

    private static ContextMenuCapabilityDefinition CreateDefinition(string script)
    {
        return new ContextMenuCapabilityDefinition
        {
            Id = "custom-command",
            Name = "命令",
            Kind = ContextMenuCapabilityKinds.PowerShell,
            RequiresExistingTargetPath = true,
            PowerShell = new PowerShellCapabilityConfig
            {
                Script = script,
                WorkingDirectoryMode = PowerShellCapabilityWorkingDirectoryModes.Target,
                ExecutionMode = PowerShellCapabilityExecutionModes.Background,
                TimeoutSeconds = 30
            }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
