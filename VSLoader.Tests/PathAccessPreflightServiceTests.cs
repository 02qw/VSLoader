using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class PathAccessPreflightServiceTests
{
    [Fact]
    public void TryGetUncHost_extracts_host_from_unc_path()
    {
        var success = PathAccessPreflightService.TryGetUncHost(@"\\192.168.15.69\instances", out var host);

        Assert.True(success);
        Assert.Equal("192.168.15.69", host);
    }

    [Fact]
    public void TryGetUncHost_ignores_local_path()
    {
        var success = PathAccessPreflightService.TryGetUncHost(@"C:\instances", out var host);

        Assert.False(success);
        Assert.Equal(string.Empty, host);
    }

    [Fact]
    public async Task CheckDirectoryAsync_returns_network_failure_before_directory_check_when_unc_host_is_unreachable()
    {
        var directoryChecked = false;
        var service = new PathAccessPreflightService(
            (_, _, _) => Task.FromResult(false),
            _ =>
            {
                directoryChecked = true;
                return true;
            });

        var result = await service.CheckDirectoryAsync(@"\\192.168.15.69\instances", TimeSpan.FromMilliseconds(50));

        Assert.False(result.Success);
        Assert.Contains("网络连接失败", result.ErrorMessage);
        Assert.False(directoryChecked);
    }

    [Fact]
    public async Task CheckDirectoryAsync_allows_local_existing_directory_without_tcp_probe()
    {
        var tcpChecked = false;
        var service = new PathAccessPreflightService(
            (_, _, _) =>
            {
                tcpChecked = true;
                return Task.FromResult(false);
            },
            _ => true);

        var result = await service.CheckDirectoryAsync(@"C:\instances", TimeSpan.FromMilliseconds(50));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(tcpChecked);
    }

    [Fact]
    public async Task CheckFileAsync_returns_network_failure_before_file_check_when_unc_host_is_unreachable()
    {
        var fileChecked = false;
        var service = new PathAccessPreflightService(
            (_, _, _) => Task.FromResult(false),
            _ => true,
            _ =>
            {
                fileChecked = true;
                return true;
            });

        var result = await service.CheckFileAsync(@"\\192.168.15.69\share\manifest.json", TimeSpan.FromMilliseconds(50));

        Assert.False(result.Success);
        Assert.Contains("网络连接失败", result.ErrorMessage);
        Assert.False(fileChecked);
    }

    [Fact]
    public async Task CheckFileAsync_allows_local_existing_file_without_tcp_probe()
    {
        var tcpChecked = false;
        var service = new PathAccessPreflightService(
            (_, _, _) =>
            {
                tcpChecked = true;
                return Task.FromResult(false);
            },
            _ => false,
            _ => true);

        var result = await service.CheckFileAsync(@"C:\release\manifest.json", TimeSpan.FromMilliseconds(50));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(tcpChecked);
    }
}
