using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class UpdateCheckServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _updateTimePath;
    private readonly UpdateCheckService _service = new();

    public UpdateCheckServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _updateTimePath = Path.Combine(_rootPath, "updateTime.json");
    }

    [Fact]
    public void Missing_updateTime_initializes_rules_and_map_without_update_notice()
    {
        var rulesPath = CreateFile("rules.csv", "rules", new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc));
        var mapPath = CreateFile("map.json", "map", new DateTime(2026, 6, 1, 2, 0, 0, DateTimeKind.Utc));

        var result = _service.Check(new UpdateCheckConfig
        {
            RulesFilePath = rulesPath,
            MapFilePath = mapPath
        }, _updateTimePath, new Version(1, 7, 2));

        var state = ReadState();
        Assert.Empty(result.UpdatedItems);
        Assert.Empty(result.Failures);
        Assert.Equal(File.GetLastWriteTimeUtc(rulesPath), state.Rules.LastUsedWriteTimeUtc);
        Assert.Equal(File.GetLastWriteTimeUtc(mapPath), state.Map.LastUsedWriteTimeUtc);
    }

    [Fact]
    public void Rules_file_newer_than_baseline_returns_rules_update_without_changing_baseline()
    {
        var baseline = new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc);
        var rulesPath = CreateFile("rules.csv", "rules", baseline.AddHours(1));
        WriteState(new UpdateTimeState
        {
            Rules = new UpdateFileState { LastUsedWriteTimeUtc = baseline }
        });

        var result = _service.Check(new UpdateCheckConfig { RulesFilePath = rulesPath }, _updateTimePath, new Version(1, 7, 2));

        Assert.Contains("批量规则文件", result.UpdatedItems);
        Assert.Equal(File.GetLastWriteTimeUtc(rulesPath), result.DetectedRulesWriteTimeUtc);
        Assert.Equal(baseline, ReadState().Rules.LastUsedWriteTimeUtc);
    }

    [Fact]
    public void AcknowledgeDetectedUpdates_updates_rules_baseline_and_prevents_same_notice()
    {
        var baseline = new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc);
        var rulesPath = CreateFile("rules.csv", "rules", baseline.AddHours(1));
        WriteState(new UpdateTimeState
        {
            Rules = new UpdateFileState { LastUsedWriteTimeUtc = baseline }
        });

        var firstResult = _service.Check(new UpdateCheckConfig { RulesFilePath = rulesPath }, _updateTimePath, new Version(1, 7, 2));
        var acknowledgeResult = _service.AcknowledgeDetectedUpdates(_updateTimePath, firstResult);
        var secondResult = _service.Check(new UpdateCheckConfig { RulesFilePath = rulesPath }, _updateTimePath, new Version(1, 7, 2));

        Assert.True(acknowledgeResult.Success, acknowledgeResult.ErrorMessage);
        Assert.Equal(File.GetLastWriteTimeUtc(rulesPath), ReadState().Rules.LastUsedWriteTimeUtc);
        Assert.DoesNotContain("批量规则文件", secondResult.UpdatedItems);
    }

    [Fact]
    public void MarkRulesUsed_updates_rules_baseline()
    {
        var rulesTime = new DateTime(2026, 6, 1, 3, 0, 0, DateTimeKind.Utc);
        var rulesPath = CreateFile("rules.csv", "rules", rulesTime);

        var result = _service.MarkRulesUsed(new UpdateCheckConfig { RulesFilePath = rulesPath }, _updateTimePath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(File.GetLastWriteTimeUtc(rulesPath), ReadState().Rules.LastUsedWriteTimeUtc);
    }

    [Fact]
    public void Map_file_newer_than_baseline_returns_map_update()
    {
        var baseline = new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc);
        var mapPath = CreateFile("map.json", "map", baseline.AddHours(1));
        WriteState(new UpdateTimeState
        {
            Map = new UpdateFileState { LastUsedWriteTimeUtc = baseline }
        });

        var result = _service.Check(new UpdateCheckConfig { MapFilePath = mapPath }, _updateTimePath, new Version(1, 7, 2));

        Assert.Contains("地图配置文件", result.UpdatedItems);
        Assert.Equal(File.GetLastWriteTimeUtc(mapPath), result.DetectedMapWriteTimeUtc);
    }

    [Fact]
    public void AcknowledgeDetectedUpdates_updates_map_baseline_and_prevents_same_notice()
    {
        var baseline = new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc);
        var mapPath = CreateFile("map.json", "map", baseline.AddHours(1));
        WriteState(new UpdateTimeState
        {
            Map = new UpdateFileState { LastUsedWriteTimeUtc = baseline }
        });

        var firstResult = _service.Check(new UpdateCheckConfig { MapFilePath = mapPath }, _updateTimePath, new Version(1, 7, 2));
        var acknowledgeResult = _service.AcknowledgeDetectedUpdates(_updateTimePath, firstResult);
        var secondResult = _service.Check(new UpdateCheckConfig { MapFilePath = mapPath }, _updateTimePath, new Version(1, 7, 2));

        Assert.True(acknowledgeResult.Success, acknowledgeResult.ErrorMessage);
        Assert.Equal(File.GetLastWriteTimeUtc(mapPath), ReadState().Map.LastUsedWriteTimeUtc);
        Assert.DoesNotContain("地图配置文件", secondResult.UpdatedItems);
    }

    [Fact]
    public void MarkMapUsed_updates_map_baseline()
    {
        var mapTime = new DateTime(2026, 6, 1, 3, 0, 0, DateTimeKind.Utc);
        var mapPath = CreateFile("map.json", "map", mapTime);

        var result = _service.MarkMapUsed(mapPath, _updateTimePath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(File.GetLastWriteTimeUtc(mapPath), ReadState().Map.LastUsedWriteTimeUtc);
    }

    [Fact]
    public void Software_manifest_version_greater_than_current_returns_software_update()
    {
        var manifestPath = CreateManifest("manifest.json", "1.7.3");

        var result = _service.Check(
            new UpdateCheckConfig(),
            _updateTimePath,
            new Version(1, 7, 2),
            manifestPath);

        Assert.Contains("软件版本", result.UpdatedItems);
        Assert.Equal("1.7.3", result.DetectedSoftwareVersion);
        Assert.True(!File.Exists(_updateTimePath) || ReadState().Software.LastUsedVersion != "1.7.3");
    }

    [Fact]
    public void AcknowledgeDetectedUpdates_updates_software_version_and_prevents_same_notice()
    {
        var manifestPath = CreateManifest("manifest.json", "1.7.3");
        WriteState(new UpdateTimeState
        {
            Software = new UpdateSoftwareState { LastUsedVersion = "1.7.2" }
        });

        var firstResult = _service.Check(
            new UpdateCheckConfig(),
            _updateTimePath,
            new Version(1, 7, 2),
            manifestPath);
        var acknowledgeResult = _service.AcknowledgeDetectedUpdates(_updateTimePath, firstResult);
        var secondResult = _service.Check(
            new UpdateCheckConfig(),
            _updateTimePath,
            new Version(1, 7, 2),
            manifestPath);

        Assert.True(acknowledgeResult.Success, acknowledgeResult.ErrorMessage);
        Assert.Equal("1.7.3", ReadState().Software.LastUsedVersion);
        Assert.DoesNotContain("软件版本", secondResult.UpdatedItems);
    }

    [Fact]
    public void Newer_manifest_version_than_acknowledged_returns_notice()
    {
        var manifestPath = CreateManifest("manifest.json", "1.7.4");
        WriteState(new UpdateTimeState
        {
            Software = new UpdateSoftwareState { LastUsedVersion = "1.7.3" }
        });

        var result = _service.Check(
            new UpdateCheckConfig(),
            _updateTimePath,
            new Version(1, 7, 2),
            manifestPath);

        Assert.Contains("软件版本", result.UpdatedItems);
        Assert.Equal("1.7.4", result.DetectedSoftwareVersion);
    }

    [Fact]
    public void Software_manifest_version_equal_current_updates_software_baseline()
    {
        var manifestPath = CreateManifest("manifest.json", "1.7.3");

        var result = _service.Check(
            new UpdateCheckConfig(),
            _updateTimePath,
            new Version(1, 7, 3),
            manifestPath);

        Assert.DoesNotContain("软件版本", result.UpdatedItems);
        Assert.Equal("1.7.3", ReadState().Software.LastUsedVersion);
    }

    [Fact]
    public void Missing_configured_file_returns_failure()
    {
        var missingPath = Path.Combine(_rootPath, "missing.csv");

        var result = _service.Check(new UpdateCheckConfig { RulesFilePath = missingPath }, _updateTimePath, new Version(1, 7, 2));

        Assert.Contains("rules 文件不存在", result.Failures);
    }

    [Fact]
    public void Missing_manifest_returns_failure()
    {
        var manifestPath = Path.Combine(_rootPath, "missing-manifest.json");

        var result = _service.Check(
            new UpdateCheckConfig(),
            _updateTimePath,
            new Version(1, 7, 2),
            manifestPath);

        Assert.Contains("软件更新 manifest 文件不存在", result.Failures);
    }

    [Fact]
    public void Empty_manifest_path_skips_software_check_without_failure()
    {
        var result = _service.Check(
            new UpdateCheckConfig(),
            _updateTimePath,
            new Version(1, 7, 2),
            string.Empty);

        Assert.DoesNotContain(result.Failures, failure => failure.Contains("manifest", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Failures, failure => failure.Contains("软件版本", StringComparison.Ordinal));
    }

    [Fact]
    public void Invalid_manifest_version_returns_failure()
    {
        var manifestPath = CreateManifest("manifest.json", "abc");

        var result = _service.Check(
            new UpdateCheckConfig(),
            _updateTimePath,
            new Version(1, 7, 2),
            manifestPath);

        Assert.Contains("manifest version 格式无效", result.Failures);
    }

    [Fact]
    public void Legacy_software_version_txt_is_ignored()
    {
        var result = _service.Check(
            new UpdateCheckConfig { SoftwareVersionFilePath = Path.Combine(_rootPath, "missing-version.txt") },
            _updateTimePath,
            new Version(1, 7, 2),
            string.Empty);

        Assert.DoesNotContain("软件版本文件不存在", result.Failures);
    }

    [Fact]
    public void Damaged_updateTime_returns_failure_and_does_not_overwrite_file()
    {
        File.WriteAllText(_updateTimePath, "{ broken json");
        var original = File.ReadAllText(_updateTimePath);

        var result = _service.Check(new UpdateCheckConfig(), _updateTimePath, new Version(1, 7, 2));

        Assert.Contains("updateTime.json 读取失败", result.Failures);
        Assert.Equal(original, File.ReadAllText(_updateTimePath));
    }

    private string CreateFile(string fileName, string content, DateTime writeTimeUtc)
    {
        var path = Path.Combine(_rootPath, fileName);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, writeTimeUtc);
        return path;
    }

    private string CreateManifest(string fileName, string version)
    {
        return CreateFile(fileName, $$"""
        {
          "version": "{{version}}",
          "packageFile": "VSLoader.zip",
          "sha256": "abc",
          "releaseNotes": ""
        }
        """, DateTime.UtcNow);
    }

    private void WriteState(UpdateTimeState state)
    {
        File.WriteAllText(_updateTimePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private UpdateTimeState ReadState()
    {
        return JsonSerializer.Deserialize<UpdateTimeState>(File.ReadAllText(_updateTimePath))!;
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
