namespace VSLoader.Models;

public static class FactoryMapPortKinds
{
    public const string Top = "top";
    public const string Right = "right";
    public const string Bottom = "bottom";
    public const string Left = "left";

    public static IReadOnlyList<string> All { get; } = [Top, Right, Bottom, Left];
}
