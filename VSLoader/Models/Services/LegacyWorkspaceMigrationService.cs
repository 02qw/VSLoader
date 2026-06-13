using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class LegacyWorkspaceMigrationService
{
    private readonly string appDataDirectory;
    private readonly WorkspaceService workspaceService;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LegacyWorkspaceMigrationService(WorkspaceService workspaceService)
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VSLoader"), workspaceService)
    {
    }

    public LegacyWorkspaceMigrationService(string appDataDirectory, WorkspaceService workspaceService)
    {
        this.appDataDirectory = appDataDirectory;
        this.workspaceService = workspaceService;
    }

    public SaveResult TryMigrate(AppSettings settings)
    {
        if (settings.MigrationCompleted)
        {
            return SaveResult.Ok();
        }

        var legacyConfigPath = Path.Combine(appDataDirectory, "config.json");
        if (!File.Exists(legacyConfigPath))
        {
            workspaceService.EnsureDefaultWorkspace(settings);
            settings.MigrationCompleted = true;
            return SaveResult.Ok();
        }

        try
        {
            var defaultContext = workspaceService.EnsureDefaultWorkspace(settings);
            CopyLegacyConfigIfTargetIsMissingOrDefault(legacyConfigPath, defaultContext.ConfigPath);
            CopyFileIfTargetMissing(
                Path.Combine(appDataDirectory, "window-layout.json"),
                defaultContext.WindowLayoutPath);
            CopyFileIfTargetMissing(
                Path.Combine(appDataDirectory, "factory-map.layout.json"),
                defaultContext.FactoryMapLayoutPath);
            CopyDirectoryIfTargetMissing(
                Path.Combine(appDataDirectory, "UIdownload"),
                defaultContext.UiDownloadDirectory);

            if (string.IsNullOrWhiteSpace(settings.VSCodePath))
            {
                var legacyConfig = JsonSerializer.Deserialize<AppConfig>(
                    File.ReadAllText(legacyConfigPath),
                    jsonOptions);
                if (!string.IsNullOrWhiteSpace(legacyConfig?.VSCodePath))
                {
                    settings.VSCodePath = legacyConfig.VSCodePath;
                }
            }

            settings.LastWorkspaceId = defaultContext.Id;
            settings.MigrationCompleted = true;
            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
        }
    }

    private static void CopyFileIfTargetMissing(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath) || File.Exists(targetPath))
        {
            return;
        }

        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        File.Copy(sourcePath, targetPath, false);
    }

    private static void CopyLegacyConfigIfTargetIsMissingOrDefault(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        if (!File.Exists(targetPath))
        {
            File.Copy(sourcePath, targetPath, false);
            return;
        }

        AppConfig? targetConfig;
        try
        {
            var targetText = File.ReadAllText(targetPath);
            targetConfig = JsonSerializer.Deserialize<AppConfig>(targetText);
        }
        catch
        {
            return;
        }

        if (targetConfig is null
            || targetConfig.Shortcuts.Count > 0
            || !string.IsNullOrWhiteSpace(targetConfig.VSCodePath))
        {
            return;
        }

        File.Copy(sourcePath, targetPath, true);
    }

    private static void CopyDirectoryIfTargetMissing(string sourceDirectory, string targetDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var targetPath = Path.Combine(targetDirectory, relativePath);
            if (!File.Exists(targetPath))
            {
                File.Copy(file, targetPath, false);
            }
        }
    }
}
