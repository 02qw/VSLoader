using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class ConfigService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public string ConfigDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "VSLoader");
        }
    }

    public string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public ConfigLoadResult Load()
    {
        Directory.CreateDirectory(ConfigDirectory);

        if (!File.Exists(ConfigPath))
        {
            var defaultConfig = new AppConfig();
            var saveResult = Save(defaultConfig);
            return saveResult.Success
                ? new ConfigLoadResult(defaultConfig, true, null)
                : new ConfigLoadResult(defaultConfig, false, saveResult.ErrorMessage);
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);

            if (config is null)
            {
                return new ConfigLoadResult(new AppConfig(), false, "配置文件为空或格式无效。");
            }

            config.VSCodePath ??= string.Empty;
            config.Shortcuts ??= new List<ShortcutItem>();
            config.AdminUi ??= new AdminUiConfig();

            return new ConfigLoadResult(config, true, null);
        }
        catch (Exception ex)
        {
            return new ConfigLoadResult(new AppConfig(), false, ex.Message);
        }
    }

    public SaveResult Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(ConfigPath, json);
            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
        }
    }
}
