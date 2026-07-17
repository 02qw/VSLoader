using System.IO;
using VSLoader.Models;

namespace VSLoader.Services;

public static class ContextMenuCapabilityVariableService
{
    public static IReadOnlyDictionary<string, string> BuildTemplateVariables(ContextMenuCapabilityExecutionContext context)
    {
        var shortcut = context.Shortcut ?? new ShortcutItem();
        var identity = ShortcutTargetIdentityParser.Parse(shortcut.TargetPath);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TargetPath"] = shortcut.TargetPath ?? string.Empty,
            ["TargetParent"] = GetTargetParent(shortcut.TargetPath),
            ["TargetName"] = identity.TargetName,
            ["InstanceId"] = identity.InstanceId,
            ["DeviceCode"] = identity.DeviceCode,
            ["DeviceType"] = identity.DeviceType,
            ["DeviceNumber"] = identity.DeviceNumber,
            ["ShortcutName"] = shortcut.Name ?? string.Empty,
            ["Description"] = shortcut.Description ?? string.Empty,
            ["SourceModuleName"] = shortcut.SourceModuleName ?? string.Empty,
            ["WorkspaceId"] = context.WorkspaceId ?? string.Empty,
            ["WorkspacePath"] = context.WorkspaceDirectory ?? string.Empty
        };
    }

    public static IReadOnlyDictionary<string, string> BuildEnvironmentVariables(ContextMenuCapabilityExecutionContext context)
    {
        var values = BuildTemplateVariables(context);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VSL_TARGET_PATH"] = values["TargetPath"],
            ["VSL_TARGET_PARENT"] = values["TargetParent"],
            ["VSL_TARGET_NAME"] = values["TargetName"],
            ["VSL_INSTANCE_ID"] = values["InstanceId"],
            ["VSL_DEVICE_CODE"] = values["DeviceCode"],
            ["VSL_DEVICE_TYPE"] = values["DeviceType"],
            ["VSL_DEVICE_NUMBER"] = values["DeviceNumber"],
            ["VSL_SHORTCUT_NAME"] = values["ShortcutName"],
            ["VSL_DESCRIPTION"] = values["Description"],
            ["VSL_SOURCE_MODULE_NAME"] = values["SourceModuleName"],
            ["VSL_WORKSPACE_ID"] = values["WorkspaceId"],
            ["VSL_WORKSPACE_PATH"] = values["WorkspacePath"],
            ["VSL_APP_BASE_PATH"] = context.AppBaseDirectory ?? string.Empty,
            ["VSL_SOURCE_SURFACE"] = context.Surface ?? string.Empty
        };
    }

    public static string GetTargetParent(string? targetPath)
    {
        var path = targetPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var root = Path.GetPathRoot(path) ?? string.Empty;
            var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.IsNullOrWhiteSpace(root)
                && string.Equals(normalized, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            return Path.GetDirectoryName(normalized) ?? root;
        }
        catch
        {
            return string.Empty;
        }
    }
}
