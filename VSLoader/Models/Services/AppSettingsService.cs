using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class AppSettingsService
{
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VSLoader"))
    {
    }

    public AppSettingsService(string appDataDirectory)
    {
        AppDataDirectory = appDataDirectory;
    }

    public string AppDataDirectory { get; }

    public string SettingsPath => Path.Combine(AppDataDirectory, "app-settings.json");

    public AppSettings LoadOrCreate(out string? warningMessage)
    {
        warningMessage = null;
        Directory.CreateDirectory(AppDataDirectory);

        if (!File.Exists(SettingsPath))
        {
            var defaultSettings = new AppSettings();
            Save(defaultSettings);
            return defaultSettings;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("程序配置文件为空。");
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(json, jsonOptions);
            if (settings is null)
            {
                throw new InvalidOperationException("程序配置文件格式无效。");
            }

            Normalize(settings);
            return settings;
        }
        catch (Exception ex)
        {
            BackupBrokenSettingsFile();
            warningMessage = $"程序配置文件损坏，已恢复默认配置：{ex.Message}";
            var defaultSettings = new AppSettings();
            Save(defaultSettings);
            return defaultSettings;
        }
    }

    public SaveResult Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDataDirectory);
            Normalize(settings);
            var json = JsonSerializer.Serialize(settings, jsonOptions);
            File.WriteAllText(SettingsPath, json);
            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
        }
    }

    private void BackupBrokenSettingsFile()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var backupPath = Path.Combine(AppDataDirectory, $"app-settings.broken.{timestamp}.json");
            File.Copy(SettingsPath, backupPath, false);
        }
        catch
        {
            // Broken program settings must not stop startup.
        }
    }

    private static void Normalize(AppSettings settings)
    {
        settings.VSCodePath ??= string.Empty;
        settings.SoftwareUpdateManifestPath ??= string.Empty;
        settings.LastWorkspaceId ??= string.Empty;
        settings.SettingsPageOrder = SettingsPageOrderService.Normalize(settings.SettingsPageOrder).ToList();
        settings.Workspaces ??= new List<WorkspaceInfo>();

        foreach (var workspace in settings.Workspaces)
        {
            workspace.Id ??= string.Empty;
            workspace.Name ??= string.Empty;
            workspace.Path ??= string.Empty;
        }
    }
}
