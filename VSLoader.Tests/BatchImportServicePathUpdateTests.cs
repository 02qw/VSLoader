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
    public void BuildPreview_skips_existing_target_path_when_generated_result_has_no_changes()
    {
        var folderPath = CreateFolder("3134_TSSP001");
        var existingShortcuts = new[]
        {
            new ShortcutItem
            {
                Name = "银烧结_001",
                TargetPath = folderPath,
                Description = "批量新增：3134_TSSP001"
            }
        };

        var items = _service.BuildPreview(_rootPath, CreateTsspRules("银烧结"), existingShortcuts);

        var item = Assert.Single(items);
        Assert.Equal(BatchImportService.StatusSkipped, item.Status);
        Assert.Equal("目标路径已存在，且当前规则结果无变化。", item.Message);
        Assert.False(item.CanImport);
        Assert.False(item.IsSelected);
        Assert.False(item.IsUpdate);
    }

    [Fact]
    public void BuildPreview_marks_existing_target_path_as_update_when_description_changes()
    {
        var folderPath = CreateFolder("3134_TSSP001");
        var existingShortcuts = new[]
        {
            new ShortcutItem
            {
                Name = "银烧结_001",
                TargetPath = folderPath,
                Description = "旧备注"
            }
        };

        var items = _service.BuildPreview(_rootPath, CreateTsspRules("银烧结"), existingShortcuts);

        var item = Assert.Single(items);
        Assert.Equal(BatchImportService.StatusUpdate, item.Status);
        Assert.True(item.CanImport);
        Assert.True(item.IsUpdate);
    }

    [Fact]
    public void BuildPreview_handles_legacy_shortcut_null_string_fields()
    {
        var folderPath = CreateFolder("3134_TSSP001");
        var existingShortcuts = new[]
        {
            new ShortcutItem
            {
                Name = "银烧结_001",
                TargetPath = folderPath,
                Description = null!,
                SourceModuleName = null!
            }
        };

        var item = Assert.Single(_service.BuildPreview(_rootPath, CreateTsspRules("银烧结"), existingShortcuts));

        Assert.Equal(BatchImportService.StatusUpdate, item.Status);
        Assert.True(item.CanImport);
        Assert.True(item.IsUpdate);
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

    [Fact]
    public void BuildPreview_marks_same_target_path_duplicates_as_cleanup()
    {
        var folderPath = CreateFolder("3134_TSSP001");
        var existingShortcuts = new[]
        {
            new ShortcutItem
            {
                Name = "amx银烧结_001",
                TargetPath = folderPath,
                Description = "批量新增：3134_TSSP001",
                UpdatedAt = new DateTime(2026, 6, 5, 10, 0, 0)
            },
            new ShortcutItem
            {
                Name = "amx银烧结_001",
                TargetPath = folderPath + Path.DirectorySeparatorChar,
                Description = "批量新增：3134_TSSP001",
                UpdatedAt = new DateTime(2026, 6, 4, 10, 0, 0)
            }
        };

        var items = _service.BuildPreview(_rootPath, CreateTsspRules("amx银烧结"), existingShortcuts);

        var item = Assert.Single(items);
        Assert.Equal(BatchImportService.StatusCleanup, item.Status);
        Assert.Equal(1, item.DuplicateCleanupCount);
        Assert.True(item.CanImport);
        Assert.True(item.IsSelected);
        Assert.True(item.IsUpdate);
    }

    [Fact]
    public void CreateApplyItems_keeps_newest_matching_duplicate_and_marks_others_for_removal()
    {
        var folderPath = CreateFolder("3134_TSSP001");
        var newer = new ShortcutItem
        {
            Name = "amx银烧结_001",
            TargetPath = folderPath,
            Description = "批量新增：3134_TSSP001",
            UpdatedAt = new DateTime(2026, 6, 5, 10, 0, 0)
        };
        var older = new ShortcutItem
        {
            Name = "amx银烧结_001",
            TargetPath = folderPath + Path.DirectorySeparatorChar,
            Description = "批量新增：3134_TSSP001",
            UpdatedAt = new DateTime(2026, 6, 4, 10, 0, 0)
        };

        var previewItem = Assert.Single(_service.BuildPreview(_rootPath, CreateTsspRules("amx银烧结"), new[] { older, newer }));
        var applyItem = Assert.Single(_service.CreateApplyItems(new[] { previewItem }));

        Assert.Equal(newer.TargetPath, applyItem.ExistingTargetPath);
        var duplicate = Assert.Single(applyItem.DuplicateShortcutsToRemove);
        Assert.Same(older, duplicate);
    }

    [Fact]
    public void CreateApplyItems_keeps_newest_duplicate_and_updates_to_current_rule_when_none_match()
    {
        var folderPath = CreateFolder("3134_TSSP001");
        var older = new ShortcutItem
        {
            Name = "银烧结_001",
            TargetPath = folderPath,
            Description = "旧备注",
            UpdatedAt = new DateTime(2026, 6, 4, 10, 0, 0)
        };
        var newer = new ShortcutItem
        {
            Name = "旧银烧结_001",
            TargetPath = folderPath + Path.DirectorySeparatorChar,
            Description = "旧备注",
            UpdatedAt = new DateTime(2026, 6, 5, 10, 0, 0)
        };

        var previewItem = Assert.Single(_service.BuildPreview(_rootPath, CreateTsspRules("amx银烧结"), new[] { older, newer }));
        var applyItem = Assert.Single(_service.CreateApplyItems(new[] { previewItem }));

        Assert.Equal(BatchImportService.StatusCleanup, previewItem.Status);
        Assert.Equal(newer.TargetPath, applyItem.ExistingTargetPath);
        Assert.Equal("amx银烧结_001", applyItem.Shortcut.Name);
        Assert.Equal("批量新增：3134_TSSP001", applyItem.Shortcut.Description);
        var duplicate = Assert.Single(applyItem.DuplicateShortcutsToRemove);
        Assert.Same(older, duplicate);
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
