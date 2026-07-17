using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ShortcutTargetIdentityParserTests
{
    [Theory]
    [InlineData(@"\\192.168.15.69\instances\3134_TSSP001", "3134_TSSP001", "3134", "TSSP001", "TSSP", "001")]
    [InlineData(@"C:\instances\5924_TSSP002\", "5924_TSSP002", "5924", "TSSP002", "TSSP", "002")]
    [InlineData(@"C:\instances\3134_tssp001", "3134_tssp001", "3134", "tssp001", "tssp", "001")]
    [InlineData(@"C:\instances\line_a_TSSP001", "line_a_TSSP001", "", "TSSP001", "TSSP", "001")]
    [InlineData(@"C:\instances\TSSP001", "TSSP001", "", "TSSP001", "TSSP", "001")]
    public void Parse_extracts_target_and_device_identity(
        string path,
        string targetName,
        string instanceId,
        string deviceCode,
        string deviceType,
        string deviceNumber)
    {
        var result = ShortcutTargetIdentityParser.Parse(path);

        Assert.Equal(targetName, result.TargetName);
        Assert.Equal(instanceId, result.InstanceId);
        Assert.Equal(deviceCode, result.DeviceCode);
        Assert.Equal(deviceType, result.DeviceType);
        Assert.Equal(deviceNumber, result.DeviceNumber);
    }

    [Theory]
    [InlineData(@"C:\instances\3134_TSSP", "3134_TSSP", "3134")]
    [InlineData(@"C:\instances\3134_001", "3134_001", "3134")]
    [InlineData(@"C:\instances\3134_设备001", "3134_设备001", "3134")]
    [InlineData(@"C:\instances\3134_TSSP-001", "3134_TSSP-001", "3134")]
    public void Parse_keeps_target_and_instance_when_device_code_is_invalid(
        string path,
        string targetName,
        string instanceId)
    {
        var result = ShortcutTargetIdentityParser.Parse(path);

        Assert.Equal(targetName, result.TargetName);
        Assert.Equal(instanceId, result.InstanceId);
        Assert.Empty(result.DeviceCode);
        Assert.Empty(result.DeviceType);
        Assert.Empty(result.DeviceNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_returns_non_null_empty_result_for_missing_path(string? path)
    {
        var result = ShortcutTargetIdentityParser.Parse(path);

        Assert.NotNull(result);
        Assert.Empty(result.TargetName);
        Assert.Empty(result.InstanceId);
        Assert.Empty(result.DeviceCode);
        Assert.Empty(result.DeviceType);
        Assert.Empty(result.DeviceNumber);
    }

    [Fact]
    public void Parse_does_not_require_network_path_to_exist()
    {
        var result = ShortcutTargetIdentityParser.Parse(
            @"\\203.0.113.250\offline-share\8842_TTVF006");

        Assert.Equal("8842_TTVF006", result.TargetName);
        Assert.Equal("8842", result.InstanceId);
        Assert.Equal("TTVF006", result.DeviceCode);
        Assert.Equal("TTVF", result.DeviceType);
        Assert.Equal("006", result.DeviceNumber);
    }
}
