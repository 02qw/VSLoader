using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class WindowLayoutService
{
    private readonly string? configDirectory;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    public WindowLayoutService()
    {
    }

    public WindowLayoutService(string configDirectory)
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

    public string LayoutPath => Path.Combine(ConfigDirectory, "window-layout.json");

    public WindowLayoutConfig LoadOrCreateDefault(Func<WindowLayoutConfig> defaultFactory, out string? warningMessage)
    {
        warningMessage = null;
        Directory.CreateDirectory(ConfigDirectory);

        if (!File.Exists(LayoutPath))
        {
            var defaultConfig = defaultFactory();
            Save(defaultConfig);
            return defaultConfig;
        }

        try
        {
            var json = File.ReadAllText(LayoutPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("窗口布局配置文件为空。");
            }

            var config = JsonSerializer.Deserialize<WindowLayoutConfig>(json, jsonOptions);
            if (config is null)
            {
                throw new InvalidOperationException("窗口布局配置文件格式无效。");
            }

            return config;
        }
        catch (Exception ex)
        {
            BackupBrokenFile();
            warningMessage = $"窗口布局配置损坏，已恢复默认布局：{ex.Message}";
            var defaultConfig = defaultFactory();
            Save(defaultConfig);
            return defaultConfig;
        }
    }

    public Task SaveAsync(WindowLayoutConfig config)
    {
        return Task.Run(() => Save(config));
    }

    public SaveResult Save(WindowLayoutConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(config, jsonOptions);
            File.WriteAllText(LayoutPath, json);
            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
        }
    }

    private void BackupBrokenFile()
    {
        try
        {
            if (!File.Exists(LayoutPath))
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var backupPath = Path.Combine(ConfigDirectory, $"window-layout.broken.{timestamp}.json");
            File.Copy(LayoutPath, backupPath, false);
        }
        catch
        {
            // A broken layout file must not prevent the app from starting.
        }
    }
}
