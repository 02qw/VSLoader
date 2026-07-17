namespace VSLoader.Models;

public static class FactoryMapJunctionAxes
{
    public const string Horizontal = "horizontal";
    public const string Vertical = "vertical";
    public const string Locked = "locked";

    public static string Normalize(string? axis)
    {
        return axis?.Trim().ToLowerInvariant() switch
        {
            Horizontal => Horizontal,
            Vertical => Vertical,
            _ => Locked
        };
    }
}
