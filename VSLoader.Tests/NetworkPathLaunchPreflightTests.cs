using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class NetworkPathLaunchPreflightTests
{
    [Fact]
    public async Task VSCodeLaunchAsync_returns_network_failure_before_path_exists_when_target_unc_host_unreachable()
    {
        var vscodePath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"), "Code.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(vscodePath)!);
        File.WriteAllText(vscodePath, string.Empty);
        var service = new VSCodeLauncherService(new PathAccessPreflightService(
            (_, _, _) => Task.FromResult(false),
            _ => throw new InvalidOperationException("directory should not be checked"),
            _ => false));

        var result = await service.LaunchAsync(vscodePath, @"\\192.168.15.69\instances\A");

        Assert.False(result.Success);
        Assert.Contains("网络连接失败", result.ErrorMessage);
    }

    [Fact]
    public async Task WebUiOpenAsync_returns_network_failure_before_properties_read_when_target_unc_host_unreachable()
    {
        var service = new WebUiService(new PathAccessPreflightService(
            (_, _, _) => Task.FromResult(false),
            _ => throw new InvalidOperationException("directory should not be checked"),
            _ => false));

        var result = await service.OpenWebUiAsync(
            new ShortcutItem { Name = "A", TargetPath = @"\\192.168.15.69\instances\A" },
            new WebUiConfig());

        Assert.False(result.Success);
        Assert.Contains("网络连接失败", result.ErrorMessage);
    }

    [Fact]
    public async Task AdminUiDownloadOneAsync_returns_network_failure_before_properties_read_when_target_unc_host_unreachable()
    {
        var service = new AdminUiService(
            Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"), "UIdownload"),
            new PathAccessPreflightService(
                (_, _, _) => Task.FromResult(false),
                _ => throw new InvalidOperationException("directory should not be checked"),
                _ => false));

        var result = await service.DownloadOneAsync(
            new ShortcutItem { Name = "A", TargetPath = @"\\192.168.15.69\instances\A" },
            new AdminUiConfig());

        Assert.False(result.Success);
        Assert.Contains("网络连接失败", result.ErrorMessage);
    }
}
