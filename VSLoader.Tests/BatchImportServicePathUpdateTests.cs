using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class BatchImportServicePathUpdateTests : IDisposable
{
    private readonly string _rootPath;
    private readonly BatchImportService _service = new();

    public BatchImportServicePathUpdateTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void BuildPreview_marks_existing_target_path_as_update()
    {
        var folderPath = CreateFolder("3134_TSSP001");
        var existingShortcuts = new[]
        {
            new ShortcutItem
            {
                Name = "银烧结_001",
                TargetPath = folderPath
            }
        };

        var items = _service.BuildPreview(_rootPath, CreateTsspRules("amx银烧结"), existingShortcuts);

        var item = Assert.Single(items);
        Assert.Equal(BatchImportService.StatusUpdate, item.Status);
        Assert.Equal("amx银烧结_001", item.GeneratedName);
        Assert.True(item.CanImport);
        Assert.True(item.IsSelected);
        Assert.True(item.IsUpdate);
        Assert.Equal("银烧结_001", item.ExistingName);
    }

    [Fact]
    public void BuildPreview_treats_normalized_paths_as_same_target()
    {
        var folderPath = CreateFolder("3134_TSSP001");
        var existingShortcuts = new[]
        {
            new ShortcutItem
            {
                Name = "银烧结_001",
                TargetPath = folderPath.Replace('\\', '/') + "/"
            }
        };

        var items = _service.BuildPreview(_rootPath, CreateTsspRules("amx银烧结"), existingShortcuts);

        var item = Assert.Single(items);
        Assert.Equal(BatchImportService.StatusUpdate, item.Status);
        Assert.True(item.IsUpdate);
    }

    [Fact]
    public void BuildPreview_keeps_name_duplicate_when_same_name_points_to_different_path()
    {
        _ = CreateFolder("3134_TSSP001");
        var existingShortcuts = new[]
        {
            new ShortcutItem
            {
                Name = "银烧结_001",
                TargetPath = Path.Combine(_rootPath, "Other_TSSP001")
            }
        };

        var items = _service.BuildPreview(_rootPath, CreateTsspRules("银烧结"), existingShortcuts);

        var item = Assert.Single(items);
        Assert.Equal(BatchImportService.StatusDuplicate, item.Status);
        Assert.False(item.CanImport);
    }

    [Fact]
    public void CreateApplyItems_marks_selected_update_items()
    {
        var previewItems = new[]
        {
            new BatchImportPreviewItem
            {
                FolderName = "3134_TSSP001",
                TargetPath = Path.Combine(_rootPath, "3134_TSSP001"),
                GeneratedName = "amx银烧结_001",
                CanImport = true,
                IsSelected = true,
                IsUpdate = true,
                ExistingTargetPath = Path.Combine(_rootPath, "3134_TSSP001")
            }
        };

        var applyItem = Assert.Single(_service.CreateApplyItems(previewItems));

        Assert.True(applyItem.IsUpdate);
        Assert.Equal("amx银烧结_001", applyItem.Shortcut.Name);
        Assert.Equal(previewItems[0].TargetPath, applyItem.Shortcut.TargetPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }

    private string CreateFolder(string name)
    {
        var path = Path.Combine(_rootPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static IReadOnlyList<BatchImportRule> CreateTsspRules(string displayName)
    {
        return
        [
            new BatchImportRule
            {
                MatchType = "Regex",
                Pattern = @"^(?<Code>\d+)_(?<Type>TSSP)(?<No>\d+)$",
                DisplayName = displayName,
                NameTemplate = "{DisplayName}_{No}"
            }
        ];
    }
}
