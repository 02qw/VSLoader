namespace VSLoader.Models;

public sealed class HotkeyConfig
{
    public bool Enabled { get; set; }

    public bool Ctrl { get; set; }

    public bool Alt { get; set; }

    public bool Shift { get; set; }

    public string InputType { get; set; } = "Keyboard";

    public string Key { get; set; } = string.Empty;

    public HotkeyConfig Clone()
    {
        return new HotkeyConfig
        {
            Enabled = Enabled,
            Ctrl = Ctrl,
            Alt = Alt,
            Shift = Shift,
            InputType = string.IsNullOrWhiteSpace(InputType) ? "Keyboard" : InputType,
            Key = Key
        };
    }
}
