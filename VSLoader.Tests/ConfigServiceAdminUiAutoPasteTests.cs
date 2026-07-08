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
    public void Load_migrates_old_or_empty_auto_paste_title_keyword_to_processor(string keyword)
    {
        var loaded = LoadConfigWithAdminUi(new AdminUiConfig
        {
            AutoPasteWindowTitleKeyword = keyword
        });

        Assert.Equal("processor", loaded.AdminUi.AutoPasteWindowTitleKeyword);
    }

    [Fact]
    public void Load_preserves_custom_auto_paste_title_keyword()
    {
        var loaded = LoadConfigWithAdminUi(new AdminUiConfig
        {
            AutoPasteWindowTitleKeyword = "TAOI"
        });

        Assert.Equal("TAOI", loaded.AdminUi.AutoPasteWindowTitleKeyword);
    }

    [Fact]
    public void Load_restores_empty_auto_paste_process_names()
    {
        var loaded = LoadConfigWithAdminUi(new AdminUiConfig
        {
            AutoPasteProcessNames = ""
        });

        Assert.Equal("java;javaw;javaws", loaded.AdminUi.AutoPasteProcessNames);
    }

    [Fact]
    public void Load_migrates_legacy_auto_paste_delay_defaults_to_faster_safe_defaults()
    {
        var loaded = LoadConfigWithAdminUi(new AdminUiConfig
        {
            AutoPasteInitialDelayMilliseconds = 2500,
            AutoPastePollIntervalMilliseconds = 300
        });

        Assert.Equal(0, loaded.AdminUi.AutoPasteInitialDelayMilliseconds);
        Assert.Equal(50, loaded.AdminUi.AutoPastePollIntervalMilliseconds);
    }

    [Fact]
    public void Load_migrates_previous_faster_auto_paste_defaults_to_current_fast_defaults()
    {
        var loaded = LoadConfigWithAdminUi(new AdminUiConfig
        {
            AutoPasteInitialDelayMilliseconds = 800,
            AutoPastePollIntervalMilliseconds = 150
        });

        Assert.Equal(0, loaded.AdminUi.AutoPasteInitialDelayMilliseconds);
        Assert.Equal(50, loaded.AdminUi.AutoPastePollIntervalMilliseconds);
    }

    [Fact]
    public void Load_migrates_previous_200ms_auto_paste_delay_default_to_current_fast_default()
    {
        var loaded = LoadConfigWithAdminUi(new AdminUiConfig
        {
            AutoPasteInitialDelayMilliseconds = 200,
            AutoPastePollIntervalMilliseconds = 150
        });

        Assert.Equal(0, loaded.AdminUi.AutoPasteInitialDelayMilliseconds);
        Assert.Equal(50, loaded.AdminUi.AutoPastePollIntervalMilliseconds);
    }

    [Fact]
    public void Load_migrates_previous_100ms_auto_paste_delay_default_to_zero_delay_default()
    {
        var loaded = LoadConfigWithAdminUi(new AdminUiConfig
        {
            AutoPasteInitialDelayMilliseconds = 100,
            AutoPastePollIntervalMilliseconds = 150
        });

        Assert.Equal(0, loaded.AdminUi.AutoPasteInitialDelayMilliseconds);
        Assert.Equal(50, loaded.AdminUi.AutoPastePollIntervalMilliseconds);
    }

    [Fact]
    public void Load_preserves_custom_auto_paste_delay_values()
    {
        var loaded = LoadConfigWithAdminUi(new AdminUiConfig
        {
            AutoPasteInitialDelayMilliseconds = 1200,
            AutoPastePollIntervalMilliseconds = 220
        });

        Assert.Equal(1200, loaded.AdminUi.AutoPasteInitialDelayMilliseconds);
        Assert.Equal(220, loaded.AdminUi.AutoPastePollIntervalMilliseconds);
    }

    [Fact]
    public void Load_migrates_legacy_default_map_hotkey_to_alt_x()
    {
        var loaded = LoadConfigWithMapHotkey(new MapHotkeyConfig
        {
            Enabled = true,
            Key = "M"
        });

        Assert.True(loaded.MapHotkey.Enabled);
        Assert.False(loaded.MapHotkey.Ctrl);
        Assert.True(loaded.MapHotkey.Alt);
        Assert.False(loaded.MapHotkey.Shift);
        Assert.Equal("X", loaded.MapHotkey.Key);
    }

    [Fact]
    public void Load_adds_alt_modifier_to_legacy_custom_single_key_map_hotkey()
    {
        var loaded = LoadConfigWithMapHotkey(new MapHotkeyConfig
        {
            Enabled = true,
            Key = "N"
        });

        Assert.True(loaded.MapHotkey.Alt);
        Assert.Equal("N", loaded.MapHotkey.Key);
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
        var config = new AppConfig
        {
            AdminUi = adminUi
        };
        File.WriteAllText(service.ConfigPath, JsonSerializer.Serialize(config));

        var result = service.Load();

        Assert.True(result.Success, result.ErrorMessage);
        return result.Config;
    }

    private AppConfig LoadConfigWithMapHotkey(MapHotkeyConfig mapHotkey)
    {
        var service = new ConfigService(rootPath);
        var config = new AppConfig
        {
            MapHotkey = mapHotkey
        };
        File.WriteAllText(service.ConfigPath, JsonSerializer.Serialize(config));

        var result = service.Load();

        Assert.True(result.Success, result.ErrorMessage);
        return result.Config;
    }
}
