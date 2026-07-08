namespace VSLoader.Models;

public sealed class MapHotkeyConfig
{
    public bool Enabled { get; set; } = true;

    public bool Ctrl { get; set; }

    public bool Alt { get; set; } = true;

    public bool Shift { get; set; }

    public string Key { get; set; } = "X";

    public MapHotkeyConfig Clone()
    {
        return new MapHotkeyConfig
        {
            Enabled = Enabled,
            Ctrl = Ctrl,
            Alt = Alt,
            Shift = Shift,
            Key = Key
        };
    }
}
