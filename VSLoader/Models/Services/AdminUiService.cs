using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class AdminUiService
{
    private static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromSeconds(5);
    private readonly string downloadDirectory;
    private readonly PathAccessPreflightService pathAccessPreflightService;

    public AdminUiService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VSLoader", "UIdownload"))
    {
    }

    public AdminUiService(string downloadDirectory)
        : this(downloadDirectory, new PathAccessPreflightService())
    {
    }

    public AdminUiService(string downloadDirectory, PathAccessPreflightService pathAccessPreflightService)
    {
        this.downloadDirectory = downloadDirectory;
        this.pathAccessPreflightService = pathAccessPreflightService;
    }

    public string DownloadDirectory
    {
        get => downloadDirectory;
    }

    public async Task<AdminUiDownloadResult> DownloadAllAsync(
        IEnumerable<ShortcutItem> shortcuts,
        AdminUiConfig config,
        IProgress<AdminUiDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(DownloadDirectory);

        var shortcutList = shortcuts.ToList();
        var total = shortcutList.Count;
        var messages = new List<string>();
        var successCount = 0;
        var failedCount = 0;
        var completedCount = 0;

        progress?.Report(new AdminUiDownloadProgress
        {
            TotalCount = total,
            CompletedCount = 0,
            SuccessCount = 0,
            FailedCount = 0,
            Message = "准备下载。"
        });

        using var httpClient = CreateHttpClient(config.IgnoreCertificateErrors);

        foreach (var shortcut in shortcutList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new AdminUiDownloadProgress
            {
                TotalCount = total,
                CompletedCount = completedCount,
                SuccessCount = successCount,
                FailedCount = failedCount,
                CurrentShortcutName = shortcut.Name,
                Message = "正在处理。"
            });

            try
            {
                var info = await DownloadOneCoreAsync(shortcut, config, httpClient, cancellationToken);
                successCount++;

                if (!string.IsNullOrWhiteSpace(info.ServiceName)
                    && !string.Equals(info.ServiceName, info.InstanceName, StringComparison.OrdinalIgnoreCase))
                {
                    messages.Add($"{shortcut.Name}：PacService 与实例名不一致：{info.ServiceName} != {info.InstanceName}");
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                messages.Add($"{shortcut.Name}：{ex.Message}");
            }

            completedCount++;
            progress?.Report(new AdminUiDownloadProgress
            {
                TotalCount = total,
                CompletedCount = completedCount,
                SuccessCount = successCount,
                FailedCount = failedCount,
                CurrentShortcutName = shortcut.Name,
                Message = "处理完成。"
            });
        }

        return new AdminUiDownloadResult
        {
            SuccessCount = successCount,
            FailedCount = failedCount,
            Messages = messages
        };
    }

    public async Task<LaunchResult> TestConnectionAsync(
        AdminUiConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = CreateHttpClient(config.IgnoreCertificateErrors);
            using var response = await httpClient.GetAsync(config.BaseUrl, cancellationToken);

            return (int)response.StatusCode < 500
                ? LaunchResult.Ok()
                : LaunchResult.Fail($"网络连接失败，请检查 AdminUI BaseUrl、网络环境或 VPN。\n\n错误原因：HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail($"网络连接失败，请检查 AdminUI BaseUrl、网络环境或 VPN。\n\n错误原因：{ex.Message}");
        }
    }

    public async Task<LaunchResult> DownloadOneAsync(
        ShortcutItem shortcut,
        AdminUiConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(DownloadDirectory);
            using var httpClient = CreateHttpClient(config.IgnoreCertificateErrors);
            _ = await DownloadOneCoreAsync(shortcut, config, httpClient, cancellationToken);
            return LaunchResult.Ok();
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail(ex.Message);
        }
    }

    public LaunchResult OpenAdminUi(ShortcutItem shortcut, AdminUiConfig config)
    {
        try
        {
            var info = ResolveShortcutInfo(shortcut, config);
            if (!File.Exists(info.LocalJnlpPath))
            {
                return LaunchResult.Fail("未找到对应 AdminUI 文件，请先点击“自动获取连接”。");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = info.LocalJnlpPath,
                UseShellExecute = true
            });

            return LaunchResult.Ok();
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail(ex.Message);
        }
    }

    public async Task<LaunchResult> OpenAdminUiAsync(ShortcutItem shortcut, AdminUiConfig config)
    {
        try
        {
            var info = await ResolveShortcutInfoAsync(shortcut, config);
            if (!File.Exists(info.LocalJnlpPath))
            {
                return LaunchResult.Fail("未找到对应 AdminUI 文件，请先点击“自动获取连接”。");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = info.LocalJnlpPath,
                UseShellExecute = true
            });

            return LaunchResult.Ok();
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail(ex.Message);
        }
    }

    private async Task<AdminUiShortcutInfo> DownloadOneCoreAsync(
        ShortcutItem shortcut,
        AdminUiConfig config,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        var info = await ResolveShortcutInfoAsync(shortcut, config);
        var tempPath = info.LocalJnlpPath + ".tmp";

        try
        {
            await using (var responseStream = await httpClient.GetStreamAsync(info.Url, cancellationToken))
            await using (var fileStream = File.Create(tempPath))
            {
                await responseStream.CopyToAsync(fileStream, cancellationToken);
            }

            File.Move(tempPath, info.LocalJnlpPath, true);
            return info;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    private async Task<AdminUiShortcutInfo> ResolveShortcutInfoAsync(ShortcutItem shortcut, AdminUiConfig config)
    {
        if (VSCodeLauncherService.IsNetworkPath(shortcut.TargetPath))
        {
            var preflight = await pathAccessPreflightService.CheckDirectoryAsync(shortcut.TargetPath);
            if (!preflight.Success)
            {
                throw new InvalidOperationException(preflight.ErrorMessage ?? $"目标路径不存在或不可访问：{shortcut.TargetPath}");
            }
        }

        return ResolveShortcutInfo(shortcut, config);
    }

    private AdminUiShortcutInfo ResolveShortcutInfo(ShortcutItem shortcut, AdminUiConfig config)
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
        var instanceName = GetRequiredValue(properties, config.InstanceNameKey);
        var port = GetRequiredValue(properties, config.PortKey);
        var serviceName = properties.TryGetValue(config.ServiceNameKey, out var value) ? value : string.Empty;

        if (!port.All(char.IsDigit))
        {
            throw new InvalidOperationException($"{config.PortKey} 不是有效端口：{port}");
        }

        var url = BuildJnlpUrl(config, instanceName, port, serviceName);
        var localPath = Path.Combine(DownloadDirectory, $"{instanceName}.jnlp");

        return new AdminUiShortcutInfo
        {
            Shortcut = shortcut,
            InstanceName = instanceName,
            Port = port,
            ServiceName = serviceName,
            Url = url,
            LocalJnlpPath = localPath
        };
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

    private static string GetRequiredValue(Dictionary<string, string> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"缺少必填字段：{key}");
        }

        return value;
    }

    private static string BuildJnlpUrl(AdminUiConfig config, string instanceName, string port, string serviceName)
    {
        var baseUrl = config.BaseUrl.TrimEnd('/') + "/";
        var fileName = $"{Uri.EscapeDataString(instanceName)}_{Uri.EscapeDataString(config.RoleName)}.jnlp";
        var zlpServiceName = string.IsNullOrWhiteSpace(serviceName)
            ? instanceName
            : serviceName.Trim();
        var query = string.Join("&", new[]
        {
            $"host={Uri.EscapeDataString(config.Host)}",
            $"port={Uri.EscapeDataString(port)}",
            $"zlpService={Uri.EscapeDataString($"{zlpServiceName}.processor")}"
        });

        return $"{baseUrl}{fileName}?{query}";
    }

    private static HttpClient CreateHttpClient(bool ignoreCertificateErrors)
    {
        var handler = new HttpClientHandler();
        if (ignoreCertificateErrors)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return new HttpClient(handler)
        {
            Timeout = DefaultHttpTimeout
        };
    }
}
