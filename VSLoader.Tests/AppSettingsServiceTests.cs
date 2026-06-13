using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class AppSettingsServiceTests : IDisposable
{
    private readonly string _rootPath;

    public AppSettingsServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void LoadOrCreate_creates_default_app_settings_file()
    {
        var service = new AppSettingsService(_rootPath);

        var settings = service.LoadOrCreate(out var warning);

        Assert.Null(warning);
        Assert.True(File.Exists(service.SettingsPath));
        Assert.True(settings.OpenLastWorkspaceOnStartup);
        Assert.Empty(settings.VSCodePath);
        Assert.Empty(settings.Workspaces);
    }

    [Fact]
    public void Save_and_load_preserves_program_level_settings()
    {
        var service = new AppSettingsService(_rootPath);
        var settings = new AppSettings
        {
            VSCodePath = @"C:\Tools\Code.exe",
            LastWorkspaceId = "line-a",
            OpenLastWorkspaceOnStartup = true,
            MigrationCompleted = true,
            Workspaces =
            [
                new WorkspaceInfo
                {
                    Id = "line-a",
                    Name = "产线A",
                    Path = Path.Combine(_rootPath, "Workspaces", "LineA"),
                    CreatedAt = new DateTime(2026, 6, 13, 8, 0, 0),
                    UpdatedAt = new DateTime(2026, 6, 13, 9, 0, 0)
                }
            ]
        };

        var saveResult = service.Save(settings);
        var loaded = service.LoadOrCreate(out var warning);

        Assert.True(saveResult.Success, saveResult.ErrorMessage);
        Assert.Null(warning);
        Assert.Equal(@"C:\Tools\Code.exe", loaded.VSCodePath);
        Assert.Equal("line-a", loaded.LastWorkspaceId);
        Assert.True(loaded.MigrationCompleted);
        Assert.Single(loaded.Workspaces);
        Assert.Equal("产线A", loaded.Workspaces[0].Name);
    }

    [Fact]
    public void LoadOrCreate_backs_up_broken_settings_and_recovers_default()
    {
        var service = new AppSettingsService(_rootPath);
        File.WriteAllText(service.SettingsPath, "{ broken json");

        var settings = service.LoadOrCreate(out var warning);

        Assert.NotNull(warning);
        Assert.Empty(settings.Workspaces);
        Assert.Contains(Directory.GetFiles(_rootPath), path => Path.GetFileName(path).StartsWith("app-settings.broken.", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
