using System.IO;
using System.Net.Sockets;

namespace VSLoader.Services;

public sealed class PathAccessPreflightService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    private readonly Func<string, int, TimeSpan, Task<bool>> tcpProbeAsync;
    private readonly Func<string, bool> directoryExists;
    private readonly Func<string, bool> fileExists;

    public PathAccessPreflightService()
        : this(TestTcpConnectionAsync, Directory.Exists, File.Exists)
    {
    }

    public PathAccessPreflightService(
        Func<string, int, TimeSpan, Task<bool>> tcpProbeAsync,
        Func<string, bool> directoryExists)
        : this(tcpProbeAsync, directoryExists, File.Exists)
    {
    }

    public PathAccessPreflightService(
        Func<string, int, TimeSpan, Task<bool>> tcpProbeAsync,
        Func<string, bool> directoryExists,
        Func<string, bool> fileExists)
    {
        this.tcpProbeAsync = tcpProbeAsync;
        this.directoryExists = directoryExists;
        this.fileExists = fileExists;
    }

    public async Task<PathAccessPreflightResult> CheckDirectoryAsync(string path, TimeSpan? timeout = null)
    {
        return await CheckPathAsync(
            path,
            "目标父级路径不能为空。",
            "目标父级路径不存在或不可访问。",
            directoryExists,
            timeout ?? DefaultTimeout);
    }

    public async Task<PathAccessPreflightResult> CheckFileAsync(string path, TimeSpan? timeout = null)
    {
        return await CheckPathAsync(
            path,
            "文件路径不能为空。",
            $"文件不存在或不可访问：{path}",
            fileExists,
            timeout ?? DefaultTimeout);
    }

    public static bool TryGetUncHost(string path, out string host)
    {
        host = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return false;
        }

        var withoutPrefix = path[2..];
        var separatorIndex = withoutPrefix.IndexOfAny(['\\', '/']);
        host = separatorIndex < 0 ? withoutPrefix : withoutPrefix[..separatorIndex];
        host = host.Trim();
        return !string.IsNullOrWhiteSpace(host);
    }

    private async Task<PathAccessPreflightResult> CheckPathAsync(
        string path,
        string emptyMessage,
        string missingMessage,
        Func<string, bool> exists,
        TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PathAccessPreflightResult.Fail(emptyMessage);
        }

        var trimmedPath = path.Trim();
        if (TryGetUncHost(trimmedPath, out var host))
        {
            var canConnect = await tcpProbeAsync(host, 445, timeout);
            if (!canConnect)
            {
                return PathAccessPreflightResult.Fail($"网络连接失败，无法连接到 {host}。请检查网络、VPN 或共享服务器状态。");
            }
        }

        var pathExists = await RunWithTimeoutAsync(() => exists(trimmedPath), timeout);
        if (pathExists is true)
        {
            return PathAccessPreflightResult.Ok();
        }

        return PathAccessPreflightResult.Fail(missingMessage);
    }

    private static async Task<bool> TestTcpConnectionAsync(string host, int port, TimeSpan timeout)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(timeout));
            return completedTask == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool?> RunWithTimeoutAsync(Func<bool> action, TimeSpan timeout)
    {
        try
        {
            var checkTask = Task.Run(action);
            var completedTask = await Task.WhenAny(checkTask, Task.Delay(timeout));
            return completedTask == checkTask ? await checkTask : null;
        }
        catch
        {
            return false;
        }
    }
}
