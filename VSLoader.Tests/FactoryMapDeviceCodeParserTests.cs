using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapDeviceCodeParserTests
{
    [Theory]
    [InlineData(@"\\192.168.15.69\instances\5534_TCDE003", "TCDE003")]
    [InlineData(@"C:\Instances\12190_TAOI007", "TAOI007")]
    [InlineData(@"C:\Instances\TCDE003", "TCDE003")]
    [InlineData(@"C:\Instances\5534_TCDE003\", "TCDE003")]
    public void Parse_extracts_device_code(string path, string expected)
    {
        Assert.Equal(expected, FactoryMapDeviceCodeParser.Parse(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(@"C:\Instances\InvalidFolder")]
    [InlineData(@"C:\Instances\5534_TCDE003_backup")]
    public void Parse_returns_empty_when_device_code_is_unavailable(string? path)
    {
        Assert.Equal(string.Empty, FactoryMapDeviceCodeParser.Parse(path));
    }
}
