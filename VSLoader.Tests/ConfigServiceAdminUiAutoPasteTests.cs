using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ConfigServiceAdminUiAutoPasteTests : IDisposable
{
    private readonly string rootPath;

    public ConfigServiceAdminUiAutoPasteTests()
    {
        rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
    }

    [Theory]
    [InlineData("znt client")]
    [InlineData("")]
    public void Load_migrates_old_or_empty_title_keyword_to_processor(string keyword)
    {
        var loaded = LoadConfigWithAdminUi(new AdminUiConfig { AutoPasteWindowTitleKeyword = keyword });

        Assert.Equal("processor", loaded.AdminUi.AutoPasteWindowTitleKeyword);
    }

    [Fact]
    public void Load_preserves_custom_title_keyword_and_restores_empty_process_names()
    {
        var loaded = LoadConfigWithAdminUi(new AdminUiConfig
        {
            AutoPasteWindowTitleKeyword = "TAOI",
            AutoPasteProcessNames = ""
        });

        Assert.Equal("TAOI", loaded.AdminUi.AutoPasteWindowTitleKeyword);
        Assert.Equal("java;javaw;javaws", loaded.AdminUi.AutoPasteProcessNames);
    }

    [Fact]
    public void Load_ignores_removed_legacy_timing_fields()
    {
        var service = new ConfigService(rootPath);
        File.WriteAllText(service.ConfigPath, """
        {
          "AdminUi": {
            "AutoPastePasswordEnabled": true,
            "AutoPasteTimeoutSeconds": 12,
            "AutoPasteInitialDelayMilliseconds": 2500,
            "AutoPastePollIntervalMilliseconds": 300,
            "AutoPasteWindowTitleKeyword": "processor",
            "AutoPasteProcessNames": "java;javaw"
          }
        }
        """);

        var result = service.Load();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.Config.AdminUi.AutoPastePasswordEnabled);
        Assert.Equal(12, result.Config.AdminUi.AutoPasteTimeoutSeconds);
        Assert.Equal("processor", result.Config.AdminUi.AutoPasteWindowTitleKeyword);
    }

    [Fact]
    public void Load_migrates_legacy_default_map_hotkey_to_alt_x()
    {
        var service = new ConfigService(rootPath);
        var config = new AppConfig { MapHotkey = new MapHotkeyConfig { Enabled = true, Key = "M" } };
        File.WriteAllText(service.ConfigPath, JsonSerializer.Serialize(config));

        var result = service.Load();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.Config.MapHotkey.Alt);
        Assert.Equal("X", result.Config.MapHotkey.Key);
    }

    [Fact]
    public void Load_adds_default_context_menu_capabilities_to_legacy_config()
    {
        var service = new ConfigService(rootPath);
        File.WriteAllText(service.ConfigPath, "{}");

        var result = service.Load();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(4, result.Config.ContextMenuCapabilities.Items.Count);
        Assert.Equal(
            ContextMenuBuiltInActionIds.All,
            result.Config.ContextMenuCapabilities.Items.Select(item => item.BuiltInActionId));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }

    private AppConfig LoadConfigWithAdminUi(AdminUiConfig adminUi)
    {
        var service = new ConfigService(rootPath);
        File.WriteAllText(service.ConfigPath, JsonSerializer.Serialize(new AppConfig { AdminUi = adminUi }));
        var result = service.Load();
        Assert.True(result.Success, result.ErrorMessage);
        return result.Config;
    }
}
