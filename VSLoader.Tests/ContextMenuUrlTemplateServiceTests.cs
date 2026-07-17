using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ContextMenuUrlTemplateServiceTests
{
    private readonly ContextMenuUrlTemplateService service = new();

    [Fact]
    public void Build_encodes_supported_variables()
    {
        var context = new ContextMenuCapabilityExecutionContext
        {
            Shortcut = new ShortcutItem
            {
                Name = "设备 A&B",
                TargetPath = @"C:\Line 1\设备"
            },
            WorkspaceId = "default",
            WorkspaceDirectory = @"C:\Workspace"
        };

        var result = service.Build(
            "https://example.com/open?name={ShortcutName}&path={TargetPath}",
            context);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(
            "https://example.com/open?name=%E8%AE%BE%E5%A4%87%20A%26B&path=C%3A%5CLine%201%5C%E8%AE%BE%E5%A4%87",
            result.Url);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32")]
    [InlineData("relative/path")]
    [InlineData("https://example.com/{Unknown}")]
    public void Build_rejects_unsupported_or_invalid_templates(string template)
    {
        var result = service.Build(template, new ContextMenuCapabilityExecutionContext());

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void Build_rejects_urls_longer_than_limit()
    {
        var result = service.Build(
            "https://example.com/?q={Description}",
            new ContextMenuCapabilityExecutionContext
            {
                Shortcut = new ShortcutItem { Description = new string('a', 9000) }
            });

        Assert.False(result.Success);
        Assert.Contains("8192", result.ErrorMessage);
    }

    [Fact]
    public void Build_replaces_all_target_identity_variables_and_preserves_leading_zero()
    {
        var result = service.Build(
            "https://example.com/device?target={TargetName}&instance={InstanceId}&code={DeviceCode}&type={DeviceType}&number={DeviceNumber}",
            new ContextMenuCapabilityExecutionContext
            {
                Shortcut = new ShortcutItem
                {
                    TargetPath = @"\\192.168.15.69\instances\3134_TSSP001"
                }
            });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(
            "https://example.com/device?target=3134_TSSP001&instance=3134&code=TSSP001&type=TSSP&number=001",
            result.Url);
    }

    [Theory]
    [InlineData("DeviceCode", @"C:\instances\3134_TSSP", "英文字母加数字")]
    [InlineData("DeviceType", @"C:\instances\3134_001", "英文字母加数字")]
    [InlineData("DeviceNumber", @"C:\instances\3134_TSSP", "英文字母加数字")]
    [InlineData("InstanceId", @"C:\instances\line_a_TSSP001", "纯数字")]
    [InlineData("TargetName", "", "目标末级名称")]
    public void Build_rejects_referenced_target_identity_when_value_cannot_be_extracted(
        string variableName,
        string targetPath,
        string expectedReason)
    {
        var result = service.Build(
            $"https://example.com/?value={{{variableName}}}",
            new ContextMenuCapabilityExecutionContext
            {
                Shortcut = new ShortcutItem { TargetPath = targetPath }
            });

        Assert.False(result.Success);
        Assert.Contains($"{{{variableName}}}", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains(expectedReason, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_keeps_existing_optional_empty_variables_compatible()
    {
        var result = service.Build(
            "https://example.com/?description={Description}&module={SourceModuleName}",
            new ContextMenuCapabilityExecutionContext { Shortcut = new ShortcutItem() });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("https://example.com/?description=&module=", result.Url);
    }
}
