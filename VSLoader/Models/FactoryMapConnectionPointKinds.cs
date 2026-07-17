namespace VSLoader.Models;

public static class FactoryMapConnectionPointKinds
{
    public const string Attached = "attached";
    public const string Free = "free";
    public const string Bend = "bend";
    public const string Junction = "junction";

    public static string Normalize(string? kind)
    {
        return kind?.Trim().ToLowerInvariant() switch
        {
            Attached => Attached,
            Bend => Bend,
            Junction => Junction,
            _ => Free
        };
    }
}
