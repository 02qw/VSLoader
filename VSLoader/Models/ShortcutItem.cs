namespace VSLoader.Models;

public sealed class ShortcutItem
{
    public string Name { get; set; } = string.Empty;

    public string TargetPath { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ShortcutItem Clone()
    {
        return new ShortcutItem
        {
            Name = Name,
            TargetPath = TargetPath,
            Description = Description,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}
