namespace VSLoader.Models;

public static class ContextMenuCapabilityKinds
{
    public const string BuiltIn = "builtIn";
    public const string PowerShell = "powerShell";
    public const string Web = "web";

    public static bool IsSupported(string? kind)
    {
        return string.Equals(kind, BuiltIn, StringComparison.Ordinal)
            || string.Equals(kind, PowerShell, StringComparison.Ordinal)
            || string.Equals(kind, Web, StringComparison.Ordinal);
    }
}
