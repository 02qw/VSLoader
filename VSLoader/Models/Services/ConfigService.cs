using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class ConfigService
{
    private readonly string? configDirectory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public ConfigService()
    {
    }

    public ConfigService(string configDirectory)
    {
        this.configDirectory = configDirectory;
    }

    public string ConfigDirectory
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                return configDirectory;
            }

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
            if (string.IsNullOrWhiteSpace(json))
            {
                return new ConfigLoadResult(new AppConfig(), false, "配置文件为空。", true);
            }

            var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);

            if (config is null)
            {
                return new ConfigLoadResult(new AppConfig(), false, "配置文件为空或格式无效。", true);
            }

            config.VSCodePath ??= string.Empty;
            config.Shortcuts ??= new List<ShortcutItem>();
            config.AdminUi ??= new AdminUiConfig();
            config.Hotkey ??= new HotkeyConfig();
            config.BatchImport ??= new BatchImportConfig();
            config.WebUi ??= new WebUiConfig();

            return new ConfigLoadResult(config, true, null);
        }
        catch (Exception ex)
        {
            return new ConfigLoadResult(new AppConfig(), false, ex.Message, true);
        }
    }

    public SaveResult BackupInvalidConfigFile()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return SaveResult.Ok();
            }

            Directory.CreateDirectory(ConfigDirectory);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var backupPath = Path.Combine(ConfigDirectory, $"config.invalid.{timestamp}.json");
            File.Copy(ConfigPath, backupPath, false);
            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
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
