using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class LegacyWorkspaceMigrationServiceTests : IDisposable
{
    private readonly string _rootPath;

    public LegacyWorkspaceMigrationServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void TryMigrate_copies_legacy_files_to_default_workspace_and_moves_vscode_path_to_app_settings()
    {
        var legacyConfig = new AppConfig
        {
            VSCodePath = @"C:\Tools\Code.exe",
            Shortcuts =
            [
                new ShortcutItem { Name = "热贴机_001", TargetPath = @"C:\Instances\001_TSSM001" }
            ]
        };
        File.WriteAllText(Path.Combine(_rootPath, "config.json"), JsonSerializer.Serialize(legacyConfig));
        File.WriteAllText(Path.Combine(_rootPath, "window-layout.json"), "{ \"WasFactoryMapOpen\": true }");
        File.WriteAllText(Path.Combine(_rootPath, "factory-map.layout.json"), "{ \"version\": 3 }");
        var legacyDownloadDirectory = Path.Combine(_rootPath, "UIdownload");
        Directory.CreateDirectory(legacyDownloadDirectory);
        File.WriteAllText(Path.Combine(legacyDownloadDirectory, "TSSM001.jnlp"), "jnlp");

        var settings = new AppSettings();
        var workspaceService = new WorkspaceService(_rootPath);
        var migrationService = new LegacyWorkspaceMigrationService(_rootPath, workspaceService);

        var result = migrationService.TryMigrate(settings);
        var context = workspaceService.ResolveStartupWorkspace(settings);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(settings.MigrationCompleted);
        Assert.Equal(@"C:\Tools\Code.exe", settings.VSCodePath);
        Assert.True(File.Exists(context.ConfigPath));
        var migratedConfig = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(context.ConfigPath));
        Assert.NotNull(migratedConfig);
        Assert.Single(migratedConfig.Shortcuts);
        Assert.Equal("热贴机_001", migratedConfig.Shortcuts[0].Name);
        Assert.True(File.Exists(context.WindowLayoutPath));
        Assert.True(File.Exists(context.FactoryMapLayoutPath));
        Assert.True(File.Exists(Path.Combine(context.UiDownloadDirectory, "TSSM001.jnlp")));
        Assert.True(File.Exists(Path.Combine(_rootPath, "config.json")));
    }

    [Fact]
    public void TryMigrate_does_not_overwrite_existing_workspace_config()
    {
        var settings = new AppSettings();
        var workspaceService = new WorkspaceService(_rootPath);
        var context = workspaceService.EnsureDefaultWorkspace(settings);
        File.WriteAllText(context.ConfigPath, "existing");
        File.WriteAllText(Path.Combine(_rootPath, "config.json"), JsonSerializer.Serialize(new AppConfig
        {
            VSCodePath = @"C:\Tools\Code.exe"
        }));
        settings.MigrationCompleted = false;

        var migrationService = new LegacyWorkspaceMigrationService(_rootPath, workspaceService);

        var result = migrationService.TryMigrate(settings);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("existing", File.ReadAllText(context.ConfigPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
