using System.IO;
using System.Text.RegularExpressions;

namespace VSLoader.Services;

public static class FactoryMapDeviceCodeParser
{
    private static readonly Regex DeviceCodeRegex = new(@"^[A-Za-z]+[0-9]+$", RegexOptions.Compiled);

    public static string Parse(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return string.Empty;
        }

        var normalizedPath = targetPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folderName = Path.GetFileName(normalizedPath);
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return string.Empty;
        }

        var separatorIndex = folderName.LastIndexOf('_');
        var candidate = separatorIndex >= 0 && separatorIndex < folderName.Length - 1
            ? folderName[(separatorIndex + 1)..]
            : folderName;

        candidate = candidate.Trim();
        return DeviceCodeRegex.IsMatch(candidate) ? candidate : string.Empty;
    }
}
