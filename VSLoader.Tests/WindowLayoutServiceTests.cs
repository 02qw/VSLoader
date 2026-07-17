using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class WindowLayoutServiceTests : IDisposable
{
    private readonly string _rootPath;

    public WindowLayoutServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void Save_and_load_preserves_shortcut_grid_column_widths()
    {
        var service = new WindowLayoutService(_rootPath);
        var config = new WindowLayoutConfig
        {
            FactoryMapWindowState = FactoryMapWindowStateKinds.WorkspaceMaximized,
            ShortcutGridColumns = new ShortcutGridColumnLayoutConfig
            {
                Name = 240,
                Description = 320,
                SourceModuleName = 420,
                UpdatedAt = 180
            }
        };

        var saveResult = service.Save(config);
        var loaded = service.LoadOrCreateDefault(() => new WindowLayoutConfig(), out var warning);

        Assert.True(saveResult.Success, saveResult.ErrorMessage);
        Assert.Null(warning);
        Assert.Equal(FactoryMapWindowStateKinds.WorkspaceMaximized, loaded.FactoryMapWindowState);
        Assert.Equal(240, loaded.ShortcutGridColumns?.Name);
        Assert.Equal(320, loaded.ShortcutGridColumns?.Description);
        Assert.Equal(420, loaded.ShortcutGridColumns?.SourceModuleName);
        Assert.Equal(180, loaded.ShortcutGridColumns?.UpdatedAt);
    }

    [Fact]
    public void Load_allows_old_layout_without_shortcut_grid_column_widths()
    {
        var service = new WindowLayoutService(_rootPath);
        Directory.CreateDirectory(_rootPath);
        File.WriteAllText(service.LayoutPath, """
{
  "WasFactoryMapOpen": false
}
""");

        var loaded = service.LoadOrCreateDefault(() => new WindowLayoutConfig(), out var warning);

        Assert.Null(warning);
        Assert.Null(loaded.ShortcutGridColumns);
        Assert.Equal(FactoryMapWindowStateKinds.Normal, loaded.FactoryMapWindowState);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
