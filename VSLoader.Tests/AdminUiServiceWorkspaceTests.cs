using System.Net;
using System.Net.Sockets;
using System.Text;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class AdminUiServiceWorkspaceTests
{
    [Fact]
    public void Constructor_accepts_workspace_download_directory()
    {
        var downloadDirectory = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"), "UIdownload");

        var service = new AdminUiService(downloadDirectory);

        Assert.Equal(downloadDirectory, service.DownloadDirectory);
    }

    [Fact]
    public async Task DownloadOne_uses_pacservice_for_zlpservice_when_present()
    {
        using var server = new RecordingHttpServer();
        var root = CreateTempDirectory();
        var target = Path.Combine(root, "10892_CommonUI");
        var downloadDirectory = Path.Combine(root, "UIdownload");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(
            Path.Combine(target, "INSTANCE.properties"),
            """
            zam.instance.name=CommonUI
            SocketServer.Port=10021
            PacService=MacMicCommonUI
            """);

        var service = new AdminUiService(downloadDirectory);
        var shortcut = new ShortcutItem
        {
            Name = "通用界面_CommonUI",
            TargetPath = target
        };
        var config = new AdminUiConfig
        {
            BaseUrl = server.BaseUrl,
            Host = "WIN-D0UJO6N8E98.macmicst.com",
            RoleName = "Administrator"
        };

        var result = await service.DownloadOneAsync(shortcut, config);
        var requestTarget = await server.RequestTargetTask;

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("CommonUI_Administrator.jnlp", requestTarget);
        Assert.Contains("zlpService=MacMicCommonUI.processor", WebUtility.UrlDecode(requestTarget));
        Assert.DoesNotContain("zlpService=CommonUI.processor", WebUtility.UrlDecode(requestTarget));
        Assert.True(File.Exists(Path.Combine(downloadDirectory, "CommonUI.jnlp")));
    }

    [Fact]
    public async Task DownloadOne_falls_back_to_instance_name_when_pacservice_missing()
    {
        using var server = new RecordingHttpServer();
        var root = CreateTempDirectory();
        var target = Path.Combine(root, "5534_TYLC001");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(
            Path.Combine(target, "INSTANCE.properties"),
            """
            zam.instance.name=TYLC001
            SocketServer.Port=10094
            """);

        var service = new AdminUiService(Path.Combine(root, "UIdownload"));
        var shortcut = new ShortcutItem
        {
            Name = "测试_TYLC001",
            TargetPath = target
        };
        var config = new AdminUiConfig
        {
            BaseUrl = server.BaseUrl,
            Host = "WIN-D0UJO6N8E98.macmicst.com",
            RoleName = "Administrator"
        };

        var result = await service.DownloadOneAsync(shortcut, config);
        var requestTarget = await server.RequestTargetTask;

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("zlpService=TYLC001.processor", WebUtility.UrlDecode(requestTarget));
    }

    [Fact]
    public async Task DownloadOne_falls_back_to_instance_name_when_pacservice_is_blank()
    {
        using var server = new RecordingHttpServer();
        var root = CreateTempDirectory();
        var target = Path.Combine(root, "5534_TYLC001");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(
            Path.Combine(target, "INSTANCE.properties"),
            """
            zam.instance.name=TYLC001
            SocketServer.Port=10094
            PacService=   
            """);

        var service = new AdminUiService(Path.Combine(root, "UIdownload"));
        var shortcut = new ShortcutItem
        {
            Name = "测试_TYLC001",
            TargetPath = target
        };
        var config = new AdminUiConfig
        {
            BaseUrl = server.BaseUrl,
            Host = "WIN-D0UJO6N8E98.macmicst.com",
            RoleName = "Administrator"
        };

        var result = await service.DownloadOneAsync(shortcut, config);
        var requestTarget = await server.RequestTargetTask;

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("zlpService=TYLC001.processor", WebUtility.UrlDecode(requestTarget));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingHttpServer : IDisposable
    {
        private readonly TcpListener listener;
        private readonly TaskCompletionSource<string> requestTargetSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly Task serverTask;

        public RecordingHttpServer()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            BaseUrl = $"http://127.0.0.1:{port}/oistarter/";
            serverTask = Task.Run(AcceptOneAsync);
        }

        public string BaseUrl { get; }

        public Task<string> RequestTargetTask => requestTargetSource.Task;

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            listener.Stop();
            cancellationTokenSource.Dispose();
        }

        private async Task AcceptOneAsync()
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync(cancellationTokenSource.Token);
                var requestTarget = requestLine?.Split(' ')[1] ?? string.Empty;
                requestTargetSource.TrySetResult(requestTarget);

                string? line;
                do
                {
                    line = await reader.ReadLineAsync(cancellationTokenSource.Token);
                }
                while (!string.IsNullOrEmpty(line));

                var body = Encoding.UTF8.GetBytes("<jnlp></jnlp>");
                var header = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: application/x-java-jnlp-file\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Connection: close\r\n\r\n");
                await stream.WriteAsync(header, cancellationTokenSource.Token);
                await stream.WriteAsync(body, cancellationTokenSource.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                requestTargetSource.TrySetCanceled();
            }
        }
    }
}
