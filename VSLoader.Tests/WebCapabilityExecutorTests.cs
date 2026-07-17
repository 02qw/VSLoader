using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class WebCapabilityExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_opens_encoded_url_with_default_browser_launcher()
    {
        string? openedUrl = null;
        var executor = new WebCapabilityExecutor(
            new ContextMenuUrlTemplateService(),
            url =>
            {
                openedUrl = url;
                return true;
            });
        var definition = new ContextMenuCapabilityDefinition
        {
            Name = "查询",
            Kind = ContextMenuCapabilityKinds.Web,
            Web = new WebCapabilityConfig { UrlTemplate = "https://example.com/?name={ShortcutName}" }
        };

        var result = await executor.ExecuteAsync(
            definition,
            new ContextMenuCapabilityExecutionContext
            {
                Shortcut = new ShortcutItem { Name = "设备 A&B" }
            },
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("https://example.com/?name=%E8%AE%BE%E5%A4%87%20A%26B", openedUrl);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_open_browser_when_derived_variable_is_unavailable()
    {
        var browserOpened = false;
        var executor = new WebCapabilityExecutor(
            new ContextMenuUrlTemplateService(),
            _ =>
            {
                browserOpened = true;
                return true;
            });
        var definition = new ContextMenuCapabilityDefinition
        {
            Name = "设备页面",
            Kind = ContextMenuCapabilityKinds.Web,
            Web = new WebCapabilityConfig
            {
                UrlTemplate = "https://example.com/?id={DeviceCode}"
            }
        };

        var result = await executor.ExecuteAsync(
            definition,
            new ContextMenuCapabilityExecutionContext
            {
                Shortcut = new ShortcutItem { TargetPath = @"C:\instances\invalid-folder" }
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(browserOpened);
        Assert.Contains("{DeviceCode}", result.Message, StringComparison.Ordinal);
    }
}
