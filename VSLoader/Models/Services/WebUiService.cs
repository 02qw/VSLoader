using System.Diagnostics;
using System.IO;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class WebUiService
{
    private readonly PathAccessPreflightService pathAccessPreflightService;

    public WebUiService()
        : this(new PathAccessPreflightService())
    {
    }

    public WebUiService(PathAccessPreflightService pathAccessPreflightService)
    {
        this.pathAccessPreflightService = pathAccessPreflightService;
    }

    public LaunchResult OpenWebUi(ShortcutItem shortcut, WebUiConfig config)
    {
        try
        {
            var url = BuildWebUiUrl(shortcut, config);
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            return LaunchResult.Ok();
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail(ex.Message);
        }
    }

    public async Task<LaunchResult> OpenWebUiAsync(ShortcutItem shortcut, WebUiConfig config)
    {
        try
        {
            var preflight = await PreflightShortcutTargetAsync(shortcut.TargetPath);
            if (!preflight.Success)
            {
                return LaunchResult.Fail(preflight.ErrorMessage ?? $"目标路径不存在或不可访问：{shortcut.TargetPath}");
            }

            var url = BuildWebUiUrl(shortcut, config);
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            return LaunchResult.Ok();
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail(ex.Message);
        }
    }

    private static string BuildWebUiUrl(ShortcutItem shortcut, WebUiConfig config)
    {
        if (!Directory.Exists(shortcut.TargetPath))
        {
            throw new InvalidOperationException($"目标路径不存在或不可访问：{shortcut.TargetPath}");
        }

        var propertiesPath = Path.Combine(shortcut.TargetPath, config.InstancePropertiesName);
        if (!File.Exists(propertiesPath))
        {
            throw new InvalidOperationException($"未找到 {config.InstancePropertiesName}。");
        }

        var properties = ReadPropertiesFile(propertiesPath);
        var instanceName = GetInstanceName(shortcut.TargetPath, properties, config.InstanceNameKey);
        var port = GetRequiredValue(properties, config.SslPortKey, $"缺少 WebUI 端口配置：{config.SslPortKey}");

        if (!port.All(char.IsDigit))
        {
            throw new InvalidOperationException($"{config.SslPortKey} 不是有效端口：{port}");
        }

        return BuildUrl(config, instanceName, port);
    }

    private static string GetInstanceName(string targetPath, Dictionary<string, string> properties, string instanceNameKey)
    {
        if (properties.TryGetValue(instanceNameKey, out var configuredName) && !string.IsNullOrWhiteSpace(configuredName))
        {
            return configuredName;
        }

        var folderName = Path.GetFileName(targetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var separatorIndex = folderName.LastIndexOf('_');
        if (separatorIndex >= 0 && separatorIndex < folderName.Length - 1)
        {
            return folderName[(separatorIndex + 1)..];
        }

        throw new InvalidOperationException($"缺少实例名配置：{instanceNameKey}");
    }

    private static Dictionary<string, string> ReadPropertiesFile(string path)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var index = trimmed.IndexOf('=');
            if (index < 1)
            {
                continue;
            }

            var key = trimmed[..index].Trim();
            var value = trimmed[(index + 1)..].Trim();
            properties[key] = value;
        }

        return properties;
    }

    private static string GetRequiredValue(Dictionary<string, string> properties, string key, string errorMessage)
    {
        if (!properties.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value;
    }

    private static string BuildUrl(WebUiConfig config, string instanceName, string port)
    {
        var baseUrl = config.BaseUrl.TrimEnd('/');
        return $"{baseUrl}:{Uri.EscapeDataString(port)}/{Uri.EscapeDataString(instanceName)}/ui";
    }

    private async Task<PathAccessPreflightResult> PreflightShortcutTargetAsync(string targetPath)
    {
        if (VSCodeLauncherService.IsNetworkPath(targetPath))
        {
            return await pathAccessPreflightService.CheckDirectoryAsync(targetPath);
        }

        return Directory.Exists(targetPath)
            ? PathAccessPreflightResult.Ok()
            : PathAccessPreflightResult.Fail($"目标路径不存在或不可访问：{targetPath}");
    }
}
