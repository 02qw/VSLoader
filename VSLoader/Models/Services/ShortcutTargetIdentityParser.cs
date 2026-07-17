using System.IO;
using System.Text.RegularExpressions;
using VSLoader.Models;

namespace VSLoader.Services;

public static class ShortcutTargetIdentityParser
{
    private static readonly Regex DeviceCodeRegex = new(
        @"^(?<Type>[A-Za-z]+)(?<Number>[0-9]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex InstanceIdRegex = new(
        @"^[0-9]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ShortcutTargetIdentity Parse(string? targetPath)
    {
        var path = targetPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ShortcutTargetIdentity();
        }

        try
        {
            var normalizedPath = path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var targetName = Path.GetFileName(normalizedPath);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return new ShortcutTargetIdentity();
            }

            var separatorIndex = targetName.LastIndexOf('_');
            var deviceCodeCandidate = separatorIndex >= 0
                ? targetName[(separatorIndex + 1)..].Trim()
                : targetName.Trim();
            var instanceIdCandidate = separatorIndex > 0
                ? targetName[..separatorIndex].Trim()
                : string.Empty;
            var deviceMatch = DeviceCodeRegex.Match(deviceCodeCandidate);

            return new ShortcutTargetIdentity
            {
                TargetName = targetName,
                InstanceId = InstanceIdRegex.IsMatch(instanceIdCandidate)
                    ? instanceIdCandidate
                    : string.Empty,
                DeviceCode = deviceMatch.Success ? deviceCodeCandidate : string.Empty,
                DeviceType = deviceMatch.Success ? deviceMatch.Groups["Type"].Value : string.Empty,
                DeviceNumber = deviceMatch.Success ? deviceMatch.Groups["Number"].Value : string.Empty
            };
        }
        catch
        {
            return new ShortcutTargetIdentity();
        }
    }
}
