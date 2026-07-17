namespace VSLoader.Services;

public static class FactoryMapDeviceCodeParser
{
    public static string Parse(string? targetPath)
    {
        return ShortcutTargetIdentityParser.Parse(targetPath).DeviceCode;
    }
}
