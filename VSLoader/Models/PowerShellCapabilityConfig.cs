namespace VSLoader.Models;

public sealed class PowerShellCapabilityConfig
{
    public string Script { get; set; } = string.Empty;

    public string WorkingDirectoryMode { get; set; } = PowerShellCapabilityWorkingDirectoryModes.Target;

    public string ExecutionMode { get; set; } = PowerShellCapabilityExecutionModes.Visible;

    public int TimeoutSeconds { get; set; } = 30;

    public PowerShellCapabilityConfig Clone()
    {
        return new PowerShellCapabilityConfig
        {
            Script = Script,
            WorkingDirectoryMode = WorkingDirectoryMode,
            ExecutionMode = ExecutionMode,
            TimeoutSeconds = TimeoutSeconds
        };
    }
}

public static class PowerShellCapabilityExecutionModes
{
    public const string Visible = "visible";
    public const string Background = "background";

    public static bool IsSupported(string? mode)
    {
        return string.Equals(mode, Visible, StringComparison.Ordinal)
            || string.Equals(mode, Background, StringComparison.Ordinal);
    }
}

public static class PowerShellCapabilityWorkingDirectoryModes
{
    public const string Target = "target";
    public const string TargetParent = "targetParent";
    public const string Workspace = "workspace";
    public const string App = "app";

    public static bool IsSupported(string? mode)
    {
        return string.Equals(mode, Target, StringComparison.Ordinal)
            || string.Equals(mode, TargetParent, StringComparison.Ordinal)
            || string.Equals(mode, Workspace, StringComparison.Ordinal)
            || string.Equals(mode, App, StringComparison.Ordinal);
    }
}
