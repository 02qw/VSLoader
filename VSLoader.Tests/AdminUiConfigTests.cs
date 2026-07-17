using VSLoader.Models;

namespace VSLoader.Tests;

public sealed class AdminUiConfigTests
{
    [Fact]
    public void Auto_paste_defaults_enable_password_paste()
    {
        var config = new AdminUiConfig();

        Assert.True(config.AutoPastePasswordEnabled);
        Assert.Equal(12, config.AutoPasteTimeoutSeconds);
        Assert.Equal("processor", config.AutoPasteWindowTitleKeyword);
        Assert.Equal("java;javaw;javaws", config.AutoPasteProcessNames);
    }

    [Fact]
    public void Clone_preserves_auto_paste_settings()
    {
        var config = new AdminUiConfig
        {
            AutoPastePasswordEnabled = true,
            AutoPasteTimeoutSeconds = 22,
            AutoPasteWindowTitleKeyword = "Admin",
            AutoPasteProcessNames = "javaw;custom"
        };

        var clone = config.Clone();

        Assert.True(clone.AutoPastePasswordEnabled);
        Assert.Equal(22, clone.AutoPasteTimeoutSeconds);
        Assert.Equal("Admin", clone.AutoPasteWindowTitleKeyword);
        Assert.Equal("javaw;custom", clone.AutoPasteProcessNames);
    }
}
