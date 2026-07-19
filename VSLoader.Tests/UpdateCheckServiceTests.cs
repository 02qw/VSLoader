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
    public void Missing_updateTime_initializes_global_config_without_update_notice()
    {
        var exportedAt = new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.FromHours(8));
        var packagePath = CreateGlobalConfigPackage("global-config.json", exportedAt, new DateTime(2026, 7, 6, 17, 0, 0, DateTimeKind.Utc));

        var result = _service.Check(new UpdateCheckConfig
        {
            GlobalConfigPackagePath = packagePath
        }, _updateTimePath, new Version(1, 7, 2));

        var state = ReadState();
        Assert.Empty(result.UpdatedItems);
        Assert.Empty(result.Failures);
        Assert.Equal(File.GetLastWriteTimeUtc(packagePath), state.GlobalConfig.LastSeenWriteTimeUtc);
        Assert.Equal(exportedAt, state.GlobalConfig.LastUsedExportedAt);
    }

    [Fact]
    public void MigrateLegacyUpdateTimeFiles_merges_the_newest_baseline_without_downgrade()
    {
        var globalPath = Path.Combine(_rootPath, "global-updateTime.json");
        var olderPath = Path.Combine(_rootPath, "workspace-a", "updateTime.json");
        var newerPath = Path.Combine(_rootPath, "workspace-b", "updateTime.json");
        Directory.CreateDirectory(Path.GetDirectoryName(olderPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(newerPath)!);

        var older = new UpdateTimeState
        {
            GlobalConfig = new UpdateGlobalConfigState
            {
                LastSeenWriteTimeUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUsedExportedAt = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.FromHours(8))
            },
            Software = new UpdateSoftwareState { LastUsedVersion = "4.0.0" }
        };
        var newer = new UpdateTimeState
        {
            GlobalConfig = new UpdateGlobalConfigState
            {
                LastSeenWriteTimeUtc = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
                LastUsedExportedAt = new DateTimeOffset(2026, 7, 2, 8, 0, 0, TimeSpan.FromHours(8))
            },
            Software = new UpdateSoftwareState { LastUsedVersion = "4.1.0" }
        };
        WriteStateTo(olderPath, older);
        WriteStateTo(newerPath, newer);

        var olderPackagePath = Path.Combine(_rootPath, "package-a.json");
        var newerPackagePath = Path.Combine(_rootPath, "package-b.json");
        var result = _service.MigrateLegacyUpdateTimeFiles(globalPath,
        [
            new LegacyUpdateTimeSource(olderPath, olderPackagePath),
            new LegacyUpdateTimeSource(newerPath, newerPackagePath)
        ]);

        Assert.True(result.Success, result.ErrorMessage);
        var migrated = ReadStateFrom(globalPath);
        Assert.Equal(older.GlobalConfig.LastUsedExportedAt, migrated.GlobalConfigs[Path.GetFullPath(olderPackagePath)].LastUsedExportedAt);
        Assert.Equal(newer.GlobalConfig.LastUsedExportedAt, migrated.GlobalConfigs[Path.GetFullPath(newerPackagePath)].LastUsedExportedAt);
        Assert.Equal("4.1.0", migrated.Software.LastUsedVersion);

        WriteStateTo(olderPath, older);
        var secondResult = _service.MigrateLegacyUpdateTimeFiles(globalPath,
            [new LegacyUpdateTimeSource(olderPath, olderPackagePath)]);

        Assert.True(secondResult.Success, secondResult.ErrorMessage);
        Assert.Equal("4.1.0", ReadStateFrom(globalPath).Software.LastUsedVersion);
    }

    [Fact]
    public void Global_config_baselines_are_isolated_by_package_path()
    {
        var packageA = CreateGlobalConfigPackage("package-a.json",
            new DateTimeOffset(2026, 7, 8, 1, 0, 0, TimeSpan.FromHours(8)),
            new DateTime(2026, 7, 7, 17, 0, 0, DateTimeKind.Utc));
        var packageB = CreateGlobalConfigPackage("package-b.json",
            new DateTimeOffset(2026, 7, 9, 1, 0, 0, TimeSpan.FromHours(8)),
            new DateTime(2026, 7, 8, 17, 0, 0, DateTimeKind.Utc));
        WriteState(new UpdateTimeState
        {
            GlobalConfigs = new Dictionary<string, UpdateGlobalConfigState>(StringComparer.OrdinalIgnoreCase)
            {
                [Path.GetFullPath(packageA)] = new UpdateGlobalConfigState
                {
                    LastSeenWriteTimeUtc = File.GetLastWriteTimeUtc(packageA).AddHours(-1),
                    LastUsedExportedAt = new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.FromHours(8))
                },
                [Path.GetFullPath(packageB)] = new UpdateGlobalConfigState
                {
                    LastSeenWriteTimeUtc = File.GetLastWriteTimeUtc(packageB),
                    LastUsedExportedAt = new DateTimeOffset(2026, 7, 9, 1, 0, 0, TimeSpan.FromHours(8))
                }
            }
        });

        var resultA = _service.Check(new UpdateCheckConfig { GlobalConfigPackagePath = packageA }, _updateTimePath, new Version(4, 0));
        var resultB = _service.Check(new UpdateCheckConfig { GlobalConfigPackagePath = packageB }, _updateTimePath, new Version(4, 0));

        Assert.Contains("全局配置", resultA.UpdatedItems);
        Assert.Equal(Path.GetFullPath(packageA), resultA.DetectedGlobalConfigPath);
        Assert.DoesNotContain("全局配置", resultB.UpdatedItems);
    }

    [Fact]
    public void MarkGlobalConfigImported_seeds_configured_path_from_imported_package_time()
    {
        var importedPackage = CreateGlobalConfigPackage("downloaded-package.json",
            new DateTimeOffset(2026, 7, 10, 1, 0, 0, TimeSpan.FromHours(8)),
            new DateTime(2026, 7, 9, 17, 0, 0, DateTimeKind.Utc));
        var configuredPackage = CreateGlobalConfigPackage("server-package.json",
            new DateTimeOffset(2026, 7, 11, 1, 0, 0, TimeSpan.FromHours(8)),
            new DateTime(2026, 7, 10, 17, 0, 0, DateTimeKind.Utc));

        var markResult = _service.MarkGlobalConfigImported(
            importedPackage,
            configuredPackage,
            _updateTimePath);

        var checkResult = _service.Check(
            new UpdateCheckConfig { GlobalConfigPackagePath = configuredPackage },
            _updateTimePath,
            new Version(4, 0));

        Assert.True(markResult.Success, markResult.ErrorMessage);
        Assert.Contains("全局配置", checkResult.UpdatedItems);
        Assert.Equal(Path.GetFullPath(configuredPackage), checkResult.DetectedGlobalConfigPath);
    }

    [Fact]
    public void Global_config_package_new_exported_at_shows_update_notice()
    {
        var baselineExportedAt = new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.FromHours(8));
        var newerExportedAt = baselineExportedAt.AddHours(2);
        var writeTimeUtc = new DateTime(2026, 7, 6, 19, 0, 0, DateTimeKind.Utc);
        var packagePath = CreateGlobalConfigPackage("global-config.json", newerExportedAt, writeTimeUtc);
        WriteState(new UpdateTimeState
        {
            GlobalConfig = new UpdateGlobalConfigState
            {
                LastSeenWriteTimeUtc = writeTimeUtc.AddHours(-1),
                LastUsedExportedAt = baselineExportedAt
            }
        });

        var result = _service.Check(new UpdateCheckConfig
        {
            GlobalConfigPackagePath = packagePath
        }, _updateTimePath, new Version(1, 7, 2));

        Assert.Contains("全局配置", result.UpdatedItems);
        Assert.Equal(newerExportedAt, result.DetectedGlobalConfigExportedAt);
        Assert.Equal(File.GetLastWriteTimeUtc(packagePath), result.DetectedGlobalConfigWriteTimeUtc);
        Assert.Equal(baselineExportedAt, ReadState().GlobalConfig.LastUsedExportedAt);
    }

    [Fact]
    public void AcknowledgeDetectedUpdates_updates_global_config_baseline()
    {
        var baselineExportedAt = new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.FromHours(8));
        var newerExportedAt = baselineExportedAt.AddHours(2);
        var writeTimeUtc = new DateTime(2026, 7, 6, 19, 0, 0, DateTimeKind.Utc);
        WriteState(new UpdateTimeState
        {
            GlobalConfig = new UpdateGlobalConfigState
            {
                LastSeenWriteTimeUtc = writeTimeUtc.AddHours(-1),
                LastUsedExportedAt = baselineExportedAt
            }
        });
        var result = new UpdateCheckResult
        {
            DetectedGlobalConfigExportedAt = newerExportedAt,
            DetectedGlobalConfigWriteTimeUtc = writeTimeUtc
        };
        result.UpdatedItems.Add("全局配置");

        var acknowledgeResult = _service.AcknowledgeDetectedUpdates(_updateTimePath, result);

        var state = ReadState();
        Assert.True(acknowledgeResult.Success, acknowledgeResult.ErrorMessage);
        Assert.Equal(newerExportedAt, state.GlobalConfig.LastUsedExportedAt);
        Assert.Equal(writeTimeUtc, state.GlobalConfig.LastSeenWriteTimeUtc);
    }

    [Fact]
    public void MarkGlobalConfigUsed_updates_global_config_baseline()
    {
        var exportedAt = new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.FromHours(8));
        var packagePath = CreateGlobalConfigPackage("global-config.json", exportedAt, new DateTime(2026, 7, 6, 17, 0, 0, DateTimeKind.Utc));

        var result = _service.MarkGlobalConfigUsed(packagePath, _updateTimePath);

        var state = ReadState();
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(File.GetLastWriteTimeUtc(packagePath), state.GlobalConfig.LastSeenWriteTimeUtc);
        Assert.Equal(exportedAt, state.GlobalConfig.LastUsedExportedAt);
    }

    [Fact]
    public void Global_config_unchanged_write_time_does_not_read_json()
    {
        var writeTimeUtc = new DateTime(2026, 7, 6, 17, 0, 0, DateTimeKind.Utc);
        var packagePath = CreateFile("global-config.json", "{ broken json", writeTimeUtc);
        WriteState(new UpdateTimeState
        {
            GlobalConfig = new UpdateGlobalConfigState
            {
                LastSeenWriteTimeUtc = writeTimeUtc,
                LastUsedExportedAt = new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.FromHours(8))
            }
        });

        var result = _service.Check(new UpdateCheckConfig
        {
            GlobalConfigPackagePath = packagePath
        }, _updateTimePath, new Version(1, 7, 2));

        Assert.Empty(result.UpdatedItems);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Global_config_write_time_changed_but_exported_at_same_does_not_show_update()
    {
        var exportedAt = new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.FromHours(8));
        var writeTimeUtc = new DateTime(2026, 7, 6, 18, 0, 0, DateTimeKind.Utc);
        var packagePath = CreateGlobalConfigPackage("global-config.json", exportedAt, writeTimeUtc);
        WriteState(new UpdateTimeState
        {
            GlobalConfig = new UpdateGlobalConfigState
            {
                LastSeenWriteTimeUtc = writeTimeUtc.AddHours(-1),
                LastUsedExportedAt = exportedAt
            }
        });

        var result = _service.Check(new UpdateCheckConfig
        {
            GlobalConfigPackagePath = packagePath
        }, _updateTimePath, new Version(1, 7, 2));

        Assert.DoesNotContain("全局配置", result.UpdatedItems);
        Assert.Equal(File.GetLastWriteTimeUtc(packagePath), ReadState().GlobalConfig.LastSeenWriteTimeUtc);
        Assert.Equal(exportedAt, ReadState().GlobalConfig.LastUsedExportedAt);
    }

    [Fact]
    public void Global_config_invalid_package_returns_failure()
    {
        var packagePath = CreateFile("global-config.json", "{ broken json", DateTime.UtcNow);

        var result = _service.Check(new UpdateCheckConfig
        {
            GlobalConfigPackagePath = packagePath
        }, _updateTimePath, new Version(1, 7, 2));

        Assert.Contains(result.Failures, failure => failure.Contains("全局配置包格式无效", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CheckAsync_does_not_check_rules_or_map_paths()
    {
        var service = new UpdateCheckService(new PathAccessPreflightService(
            (_, _, _) => Task.FromResult(false),
            _ => true,
            _ => throw new InvalidOperationException("rules and map should not be checked")));

        var result = await service.CheckAsync(
            new UpdateCheckConfig
            {
                RulesFilePath = @"\\192.168.15.69\release\rules.csv",
                MapFilePath = @"\\192.168.15.69\release\map.json"
            },
            _updateTimePath,
            new Version(1, 7, 2));

        Assert.Empty(result.Failures);
        Assert.Empty(result.UpdatedItems);
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
    public void Missing_rules_or_map_paths_are_ignored()
    {
        var result = _service.Check(new UpdateCheckConfig
        {
            RulesFilePath = Path.Combine(_rootPath, "missing-rules.csv"),
            MapFilePath = Path.Combine(_rootPath, "missing-map.json")
        }, _updateTimePath, new Version(1, 7, 2));

        Assert.Empty(result.Failures);
        Assert.Empty(result.UpdatedItems);
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
    public async Task CheckAsync_reports_failure_without_reading_unreachable_manifest()
    {
        var service = new UpdateCheckService(new PathAccessPreflightService(
            (_, _, _) => Task.FromResult(false),
            _ => true,
            _ => throw new InvalidOperationException("manifest should not be checked")));

        var result = await service.CheckAsync(
            new UpdateCheckConfig(),
            _updateTimePath,
            new Version(1, 7, 2),
            @"\\192.168.15.69\release\manifest.json");

        Assert.Contains(result.Failures, failure => failure.Contains("软件更新 manifest 不可访问", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("网络连接失败", StringComparison.Ordinal));
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

    private string CreateGlobalConfigPackage(string fileName, DateTimeOffset exportedAt, DateTime writeTimeUtc)
    {
        return CreateFile(fileName, $$"""
        {
          "schemaVersion": 1,
          "appName": "VSLoader",
          "exportedAt": "{{exportedAt:O}}",
          "programSettings": {},
          "workspaceConfig": {},
          "factoryMapLayout": null
        }
        """, writeTimeUtc);
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
        WriteStateTo(_updateTimePath, state);
    }

    private UpdateTimeState ReadState()
    {
        return ReadStateFrom(_updateTimePath);
    }

    private static void WriteStateTo(string path, UpdateTimeState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static UpdateTimeState ReadStateFrom(string path)
    {
        return JsonSerializer.Deserialize<UpdateTimeState>(File.ReadAllText(path))!;
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
