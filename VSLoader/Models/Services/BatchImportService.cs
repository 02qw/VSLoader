using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class BatchImportService
{
    public const string StatusImportable = "可新增";
    public const string StatusUpdate = "可更新";
    public const string StatusCleanup = "可清理";
    public const string StatusSkipped = "已跳过";
    public const string StatusDuplicate = "名称重复";
    public const string StatusRuleError = "规则错误";

    private static readonly string[] RequiredHeaders =
    [
        nameof(BatchImportRule.MatchType),
        nameof(BatchImportRule.Pattern),
        nameof(BatchImportRule.DisplayName),
        nameof(BatchImportRule.NameTemplate)
    ];

    public IReadOnlyList<BatchImportRule> LoadRules(string csvPath, out List<string> errors)
    {
        errors = new List<string>();
        var validRules = new List<BatchImportRule>();

        try
        {
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null,
                HeaderValidated = null
            });

            if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
            {
                errors.Add("CSV 表头不正确，必须是 MatchType,Pattern,DisplayName,NameTemplate。");
                return validRules;
            }

            var headers = csv.HeaderRecord;
            var isSimpleModuleMapCsv = headers.Contains(nameof(BatchImportRule.ModuleName))
                && headers.Contains(nameof(BatchImportRule.DisplayName))
                && !headers.Contains(nameof(BatchImportRule.MatchType))
                && !headers.Contains(nameof(BatchImportRule.Pattern));
            var isComplexRuleCsv = RequiredHeaders.All(required => headers.Contains(required));
            if (!isSimpleModuleMapCsv && !isComplexRuleCsv)
            {
                errors.Add("CSV 表头不正确，必须是 ModuleName,DisplayName 或 MatchType,Pattern,DisplayName,NameTemplate。");
                return validRules;
            }

            var hasModulePatternHeader = headers.Contains(nameof(BatchImportRule.ModulePattern));
            var hasSimpleNameTemplateHeader = headers.Contains(nameof(BatchImportRule.NameTemplate));
            var rowNumber = 1;
            while (csv.Read())
            {
                rowNumber++;
                var rule = isSimpleModuleMapCsv
                    ? new BatchImportRule
                    {
                        MatchType = "ModuleMap",
                        ModuleName = csv.GetField(nameof(BatchImportRule.ModuleName))?.Trim() ?? string.Empty,
                        DisplayName = csv.GetField(nameof(BatchImportRule.DisplayName))?.Trim() ?? string.Empty,
                        NameTemplate = hasSimpleNameTemplateHeader
                            ? csv.GetField(nameof(BatchImportRule.NameTemplate))?.Trim() ?? string.Empty
                            : "{DisplayName}_{No}"
                    }
                    : new BatchImportRule
                    {
                        MatchType = csv.GetField(nameof(BatchImportRule.MatchType))?.Trim() ?? string.Empty,
                        Pattern = csv.GetField(nameof(BatchImportRule.Pattern))?.Trim() ?? string.Empty,
                        ModulePattern = hasModulePatternHeader
                            ? csv.GetField(nameof(BatchImportRule.ModulePattern))?.Trim() ?? string.Empty
                            : string.Empty,
                        DisplayName = csv.GetField(nameof(BatchImportRule.DisplayName))?.Trim() ?? string.Empty,
                        NameTemplate = csv.GetField(nameof(BatchImportRule.NameTemplate))?.Trim() ?? string.Empty
                    };

                var rowErrors = ValidateRule(rule, rowNumber);
                if (rowErrors.Count > 0)
                {
                    errors.AddRange(rowErrors);
                    continue;
                }

                rule.SortIndex = validRules.Count;
                validRules.Add(rule);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"CSV 读取失败：{ex.Message}");
        }

        return validRules;
    }

    public IReadOnlyList<BatchImportPreviewItem> BuildPreview(
        string parentFolderPath,
        IReadOnlyList<BatchImportRule> rules,
        IEnumerable<ShortcutItem> existingShortcuts,
        IEnumerable<string>? ruleErrors = null,
        IProgress<BatchImportScanProgress>? progress = null)
    {
        var items = new List<BatchImportPreviewItem>();

        if (ruleErrors is not null)
        {
            foreach (var error in ruleErrors)
            {
                items.Add(new BatchImportPreviewItem
                {
                    Status = StatusRuleError,
                    Message = error,
                    CanImport = false,
                    IsSelected = false,
                    SortRuleIndex = int.MaxValue,
                    SortName = error
                });
            }
        }

        var existingList = existingShortcuts.ToList();
        var existingGroupsByPath = existingList
            .Where(shortcut => !string.IsNullOrWhiteSpace(shortcut.TargetPath))
            .GroupBy(shortcut => NormalizePathKey(shortcut.TargetPath), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var existingPathKeysByName = existingList
            .Where(shortcut => !string.IsNullOrWhiteSpace(shortcut.Name))
            .GroupBy(shortcut => NormalizeNameKey(shortcut.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(shortcut => NormalizePathKey(shortcut.TargetPath))
                    .Where(pathKey => !string.IsNullOrWhiteSpace(pathKey))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        var previewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previewPathKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var isSimpleModuleMapMode = IsSimpleModuleMapMode(rules);
        progress?.Report(new BatchImportScanProgress
        {
            CompletedCount = 0,
            TotalCount = 0,
            Stage = "正在枚举子文件夹..."
        });
        var directories = Directory.EnumerateDirectories(parentFolderPath).ToList();
        var totalCount = directories.Count;

        for (var index = 0; index < directories.Count; index++)
        {
            var directory = directories[index];
            var folderName = Path.GetFileName(directory);
            progress?.Report(new BatchImportScanProgress
            {
                CompletedCount = index,
                TotalCount = totalCount,
                CurrentFolderName = folderName,
                Stage = isSimpleModuleMapMode ? "正在读取模块信息并匹配规则" : "正在匹配规则"
            });

            var pathKey = NormalizePathKey(directory);
            if (!previewPathKeys.Add(pathKey))
            {
                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    Status = StatusDuplicate,
                    Message = "本次预览中已存在相同目标路径。",
                    CanImport = false,
                    IsSelected = false,
                    SortRuleIndex = int.MaxValue,
                    SortName = folderName
                });
                ReportDirectoryCompleted(progress, index, totalCount, folderName);
                continue;
            }

            var generatedName = string.Empty;
            string? nameError = null;
            var generatedDescription = $"批量新增：{folderName}";
            int? sortNo = null;
            var matchedPattern = string.Empty;
            var sourceModuleName = string.Empty;
            var sortRuleIndex = int.MaxValue;

            if (isSimpleModuleMapMode)
            {
                var simpleResult = CreateSimpleModuleMapName(rules, folderName, directory);
                if (!simpleResult.Success)
                {
                    items.Add(new BatchImportPreviewItem
                    {
                        FolderName = folderName,
                        TargetPath = directory,
                        Status = simpleResult.Status,
                        Message = simpleResult.ErrorMessage ?? "未匹配任何规则。",
                        CanImport = false,
                        IsSelected = false,
                        SortRuleIndex = int.MaxValue - 1,
                        SortNo = simpleResult.SortNo,
                        SortName = folderName
                    });
                    ReportDirectoryCompleted(progress, index, totalCount, folderName);
                    continue;
                }

                generatedName = simpleResult.GeneratedName.Trim();
                sortNo = simpleResult.SortNo;
                matchedPattern = simpleResult.ModuleName;
                sourceModuleName = simpleResult.ModuleName;
                sortRuleIndex = simpleResult.SortRuleIndex;
            }
            else
            {
                var matchResult = FindMatchingRule(rules, folderName, directory);

                if (matchResult.Rule is null)
                {
                    items.Add(new BatchImportPreviewItem
                    {
                        FolderName = folderName,
                        TargetPath = directory,
                        Status = string.IsNullOrWhiteSpace(matchResult.ErrorMessage) ? StatusSkipped : matchResult.Status,
                        Message = string.IsNullOrWhiteSpace(matchResult.ErrorMessage) ? "未匹配任何规则。" : matchResult.ErrorMessage,
                        CanImport = false,
                        IsSelected = false,
                        SortRuleIndex = int.MaxValue - 1,
                        SortName = folderName
                    });
                    ReportDirectoryCompleted(progress, index, totalCount, folderName);
                    continue;
                }

                generatedName = GenerateName(matchResult.Rule, folderName, matchResult.RegexMatch, out nameError).Trim();
                sortNo = TryGetRegexNo(matchResult.RegexMatch);
                matchedPattern = matchResult.Rule.Pattern;
                sourceModuleName = matchResult.ModuleName;
                sortRuleIndex = matchResult.Rule.SortIndex;
            }

            if (!string.IsNullOrWhiteSpace(nameError))
            {
                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    GeneratedName = generatedName,
                    MatchedPattern = matchedPattern,
                    SourceModuleName = sourceModuleName,
                    Status = StatusRuleError,
                    Message = nameError,
                    CanImport = false,
                    IsSelected = false,
                    SortRuleIndex = sortRuleIndex,
                    SortNo = sortNo,
                    SortName = string.IsNullOrWhiteSpace(generatedName) ? folderName : generatedName
                });
                ReportDirectoryCompleted(progress, index, totalCount, folderName);
                continue;
            }

            if (string.IsNullOrWhiteSpace(generatedName))
            {
                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    MatchedPattern = matchedPattern,
                    SourceModuleName = sourceModuleName,
                    Status = StatusRuleError,
                    Message = "名称模板生成了空名称。",
                    CanImport = false,
                    IsSelected = false,
                    SortRuleIndex = sortRuleIndex,
                    SortNo = sortNo,
                    SortName = folderName
                });
                ReportDirectoryCompleted(progress, index, totalCount, folderName);
                continue;
            }

            var existingGroupForPath = existingGroupsByPath.TryGetValue(pathKey, out var matchedGroup)
                ? matchedGroup
                : [];
            var existingShortcutForPath = existingGroupForPath.Count > 0
                ? existingGroupForPath[0]
                : null;
            var hasNameConflict = HasNameConflict(existingPathKeysByName, generatedName, pathKey)
                || !previewNames.Add(generatedName);
            if (hasNameConflict)
            {
                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    GeneratedName = generatedName,
                    MatchedPattern = matchedPattern,
                    SourceModuleName = sourceModuleName,
                    Status = StatusDuplicate,
                    Message = "生成名称与已有快捷项或本次预览项目重复。",
                    CanImport = false,
                    IsSelected = false,
                    SortRuleIndex = sortRuleIndex,
                    SortNo = sortNo,
                    SortName = generatedName
                });
                ReportDirectoryCompleted(progress, index, totalCount, folderName);
                continue;
            }

            if (existingGroupForPath.Count > 1)
            {
                var keepShortcut = SelectShortcutToKeep(existingGroupForPath, generatedName, directory, generatedDescription, sourceModuleName);
                var duplicateShortcutsToRemove = existingGroupForPath
                    .Where(shortcut => !ReferenceEquals(shortcut, keepShortcut))
                    .ToList();

                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    GeneratedName = generatedName,
                    MatchedPattern = matchedPattern,
                    SourceModuleName = sourceModuleName,
                    ExistingTargetPath = keepShortcut.TargetPath,
                    ExistingName = keepShortcut.Name,
                    ExistingShortcutToUpdate = keepShortcut,
                    Status = StatusCleanup,
                    Message = $"发现 {existingGroupForPath.Count} 条相同目标路径快捷项，将保留最新规则结果并清理 {duplicateShortcutsToRemove.Count} 条重复项。",
                    CanImport = true,
                    IsSelected = true,
                    IsUpdate = true,
                    DuplicateCleanupCount = duplicateShortcutsToRemove.Count,
                    DuplicateShortcutsToRemove = duplicateShortcutsToRemove,
                    SortRuleIndex = sortRuleIndex,
                    SortNo = sortNo,
                    SortName = generatedName
                });
                ReportDirectoryCompleted(progress, index, totalCount, folderName);
                continue;
            }

            if (existingShortcutForPath is not null)
            {
                if (!HasBatchImportChanges(existingShortcutForPath, generatedName, directory, generatedDescription, sourceModuleName))
                {
                    items.Add(new BatchImportPreviewItem
                    {
                        FolderName = folderName,
                        TargetPath = directory,
                        GeneratedName = generatedName,
                        MatchedPattern = matchedPattern,
                        SourceModuleName = sourceModuleName,
                        ExistingTargetPath = existingShortcutForPath.TargetPath,
                        ExistingName = existingShortcutForPath.Name,
                        ExistingShortcutToUpdate = existingShortcutForPath,
                        Status = StatusSkipped,
                        Message = "目标路径已存在，且当前规则结果无变化。",
                        CanImport = false,
                        IsSelected = false,
                        IsUpdate = false,
                        SortRuleIndex = sortRuleIndex,
                        SortNo = sortNo,
                        SortName = generatedName
                    });
                    ReportDirectoryCompleted(progress, index, totalCount, folderName);
                    continue;
                }

                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    GeneratedName = generatedName,
                    MatchedPattern = matchedPattern,
                    SourceModuleName = sourceModuleName,
                    ExistingTargetPath = existingShortcutForPath.TargetPath,
                    ExistingName = existingShortcutForPath.Name,
                    ExistingShortcutToUpdate = existingShortcutForPath,
                    Status = StatusUpdate,
                    Message = $"目标路径已存在，将更新：{existingShortcutForPath.Name} -> {generatedName}",
                    CanImport = true,
                    IsSelected = true,
                    IsUpdate = true,
                    SortRuleIndex = sortRuleIndex,
                    SortNo = sortNo,
                    SortName = generatedName
                });
                ReportDirectoryCompleted(progress, index, totalCount, folderName);
                continue;
            }

            items.Add(new BatchImportPreviewItem
            {
                FolderName = folderName,
                TargetPath = directory,
                GeneratedName = generatedName,
                MatchedPattern = matchedPattern,
                SourceModuleName = sourceModuleName,
                Status = StatusImportable,
                Message = "可新增。",
                CanImport = true,
                IsSelected = true,
                SortRuleIndex = sortRuleIndex,
                SortNo = sortNo,
                SortName = generatedName
            });
            progress?.Report(new BatchImportScanProgress
            {
                CompletedCount = index + 1,
                TotalCount = totalCount,
                CurrentFolderName = folderName,
                Stage = "已处理"
            });
        }

        progress?.Report(new BatchImportScanProgress
        {
            CompletedCount = totalCount,
            TotalCount = totalCount,
            Stage = "扫描完成"
        });

        return items
            .OrderBy(item => GetPreviewStatusSortPriority(item.Status))
            .ThenBy(item => item.SortRuleIndex)
            .ThenBy(item => item.SortNo ?? int.MaxValue)
            .ThenBy(item => item.SortName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void ReportDirectoryCompleted(
        IProgress<BatchImportScanProgress>? progress,
        int index,
        int totalCount,
        string folderName)
    {
        progress?.Report(new BatchImportScanProgress
        {
            CompletedCount = index + 1,
            TotalCount = totalCount,
            CurrentFolderName = folderName,
            Stage = "已处理"
        });
    }

    public IReadOnlyList<ShortcutItem> CreateShortcuts(IEnumerable<BatchImportPreviewItem> previewItems)
    {
        var now = DateTime.Now;
        return previewItems
            .Where(item => item.CanImport && item.IsSelected)
            .Select(item => new ShortcutItem
            {
                Name = item.GeneratedName.Trim(),
                TargetPath = item.TargetPath.Trim(),
                Description = $"批量新增：{item.FolderName}",
                SourceModuleName = item.SourceModuleName.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();
    }

    public IReadOnlyList<BatchImportApplyItem> CreateApplyItems(IEnumerable<BatchImportPreviewItem> previewItems)
    {
        var now = DateTime.Now;
        return previewItems
            .Where(item => item.CanImport && item.IsSelected)
            .Select(item => new BatchImportApplyItem
            {
                IsUpdate = item.IsUpdate,
                ExistingTargetPath = item.ExistingTargetPath.Trim(),
                ExistingShortcutToUpdate = item.ExistingShortcutToUpdate,
                DuplicateShortcutsToRemove = item.DuplicateShortcutsToRemove,
                Shortcut = new ShortcutItem
                {
                    Name = item.GeneratedName.Trim(),
                    TargetPath = item.TargetPath.Trim(),
                    Description = $"批量新增：{item.FolderName}",
                    SourceModuleName = item.SourceModuleName.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now
                }
            })
            .ToList();
    }

    private static List<string> ValidateRule(BatchImportRule rule, int rowNumber)
    {
        var errors = new List<string>();

        if (rule.IsSimpleModuleMapRule)
        {
            if (string.IsNullOrWhiteSpace(rule.ModuleName))
            {
                errors.Add($"第 {rowNumber} 行规则错误：ModuleName 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(rule.DisplayName))
            {
                errors.Add($"第 {rowNumber} 行规则错误：DisplayName 不能为空。");
            }

            return errors;
        }

        if (string.IsNullOrWhiteSpace(rule.MatchType))
        {
            errors.Add($"第 {rowNumber} 行规则错误：MatchType 不能为空。");
        }
        else if (!IsSupportedMatchType(rule.MatchType))
        {
            errors.Add($"第 {rowNumber} 行规则错误：MatchType 只支持 Contains 或 Regex。");
        }

        if (string.IsNullOrWhiteSpace(rule.Pattern))
        {
            errors.Add($"第 {rowNumber} 行规则错误：Pattern 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(rule.DisplayName))
        {
            errors.Add($"第 {rowNumber} 行规则错误：DisplayName 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(rule.NameTemplate))
        {
            errors.Add($"第 {rowNumber} 行规则错误：NameTemplate 不能为空。");
        }

        if (string.Equals(rule.MatchType, "Regex", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(rule.Pattern))
        {
            try
            {
                _ = new Regex(rule.Pattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException ex)
            {
                errors.Add($"第 {rowNumber} 行规则错误：Regex 语法无效：{ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(rule.ModulePattern))
        {
            try
            {
                _ = new Regex(rule.ModulePattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException ex)
            {
                errors.Add($"第 {rowNumber} 行规则错误：ModulePattern Regex 语法无效：{ex.Message}");
            }
        }

        return errors;
    }

    private static RuleMatchResult FindMatchingRule(IEnumerable<BatchImportRule> rules, string folderName, string targetDirectory)
    {
        string? moduleName = null;
        string? moduleReadError = null;
        var hasFolderCandidateWithModulePattern = false;

        foreach (var rule in rules)
        {
            if (string.Equals(rule.MatchType, "Contains", StringComparison.OrdinalIgnoreCase)
                && folderName.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(rule.ModulePattern))
                {
                    return new RuleMatchResult(rule, null, null, StatusSkipped, string.Empty);
                }

                hasFolderCandidateWithModulePattern = true;
                if (!TryEnsureModuleName(targetDirectory, ref moduleName, ref moduleReadError))
                {
                    continue;
                }

                if (Regex.IsMatch(moduleName!, rule.ModulePattern, RegexOptions.IgnoreCase))
                {
                    return new RuleMatchResult(rule, null, null, StatusSkipped, moduleName ?? string.Empty);
                }

                continue;
            }

            if (string.Equals(rule.MatchType, "Regex", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(folderName, rule.Pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (string.IsNullOrWhiteSpace(rule.ModulePattern))
                    {
                        return new RuleMatchResult(rule, match, null, StatusSkipped, string.Empty);
                    }

                    hasFolderCandidateWithModulePattern = true;
                    if (!TryEnsureModuleName(targetDirectory, ref moduleName, ref moduleReadError))
                    {
                        continue;
                    }

                    if (Regex.IsMatch(moduleName!, rule.ModulePattern, RegexOptions.IgnoreCase))
                    {
                        return new RuleMatchResult(rule, match, null, StatusSkipped, moduleName ?? string.Empty);
                    }
                }
            }
        }

        if (hasFolderCandidateWithModulePattern)
        {
            if (!string.IsNullOrWhiteSpace(moduleReadError))
            {
                var status = moduleReadError.StartsWith("ZAM-DEPLOY.xml 解析失败", StringComparison.Ordinal)
                    ? StatusRuleError
                    : StatusSkipped;
                return new RuleMatchResult(null, null, moduleReadError, status, moduleName ?? string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(moduleName))
            {
                return new RuleMatchResult(null, null, $"模块名未匹配任何规则：{moduleName}", StatusSkipped, moduleName);
            }
        }

        return new RuleMatchResult(null, null, null, StatusSkipped, string.Empty);
    }

    private static bool IsSupportedMatchType(string matchType)
    {
        return string.Equals(matchType, "Contains", StringComparison.OrdinalIgnoreCase)
            || string.Equals(matchType, "Regex", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSimpleModuleMapMode(IReadOnlyList<BatchImportRule> rules)
    {
        return rules.Count > 0 && rules.All(rule => rule.IsSimpleModuleMapRule);
    }

    private static SimpleModuleMapResult CreateSimpleModuleMapName(
        IReadOnlyList<BatchImportRule> rules,
        string folderName,
        string targetDirectory)
    {
        var folderIdentity = TryParseFolderIdentity(folderName);
        if (folderIdentity is null)
        {
            return SimpleModuleMapResult.Fail(StatusSkipped, "文件夹名无法提取类型信息。");
        }

        var sortNo = folderIdentity.SortNo;
        var moduleName = TryReadZamModuleName(targetDirectory, out var moduleReadError);
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            var status = !string.IsNullOrWhiteSpace(moduleReadError)
                && moduleReadError.StartsWith("ZAM-DEPLOY.xml 解析失败", StringComparison.Ordinal)
                    ? StatusRuleError
                    : StatusSkipped;
            return SimpleModuleMapResult.Fail(status, moduleReadError ?? "未匹配任何规则。", sortNo);
        }

        var matchedRule = rules.FirstOrDefault(rule =>
            string.Equals(rule.ModuleName.Trim(), moduleName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (matchedRule is null)
        {
            return SimpleModuleMapResult.Fail(StatusSkipped, $"模块名未在 CSV 中配置：{moduleName}", sortNo, moduleName);
        }

        var generatedName = RenderSimpleModuleMapName(matchedRule, folderName, folderIdentity, out var nameError);
        if (!string.IsNullOrWhiteSpace(nameError))
        {
            return SimpleModuleMapResult.Fail(StatusRuleError, nameError, sortNo, moduleName);
        }

        return new SimpleModuleMapResult(
            true,
            generatedName,
            moduleName,
            sortNo,
            matchedRule.SortIndex,
            null,
            StatusImportable);
    }

    private static string RenderSimpleModuleMapName(
        BatchImportRule rule,
        string folderName,
        FolderIdentity folderIdentity,
        out string? errorMessage)
    {
        errorMessage = null;
        var template = string.IsNullOrWhiteSpace(rule.NameTemplate)
            ? "{DisplayName}_{No}"
            : rule.NameTemplate.Trim();

        // Preserve the legacy fallback for folders without a numeric suffix.
        if (string.Equals(template, "{DisplayName}_{No}", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(folderIdentity.No))
        {
            return $"{rule.DisplayName.Trim()}_{folderIdentity.Type}";
        }

        string? replacementError = null;
        var result = Regex.Replace(template, @"\{(?<name>[^{}]+)\}", tokenMatch =>
        {
            var token = tokenMatch.Groups["name"].Value;
            return token switch
            {
                "DisplayName" => rule.DisplayName.Trim(),
                "FolderName" => folderName,
                "Type" => folderIdentity.Type,
                "No" => folderIdentity.No ?? string.Empty,
                _ => ReportUnknownSimpleToken(token, ref replacementError)
            };
        });

        errorMessage = replacementError;
        return result.Trim();
    }

    private static string ReportUnknownSimpleToken(string token, ref string? errorMessage)
    {
        errorMessage ??= $"名称模板引用了不存在的变量：{token}。支持 DisplayName、FolderName、Type、No。";
        return string.Empty;
    }

    private static FolderIdentity? TryParseFolderIdentity(string folderName)
    {
        var withNoMatch = Regex.Match(folderName, @"^(?<Code>\d+)_(?<Type>[A-Za-z]+)(?<No>\d+)$", RegexOptions.IgnoreCase);
        if (withNoMatch.Success && withNoMatch.Groups["No"].Success)
        {
            var noText = withNoMatch.Groups["No"].Value;
            var sortNo = int.TryParse(noText, out var parsedNo) ? parsedNo : int.MaxValue;
            return new FolderIdentity(
                withNoMatch.Groups["Code"].Value,
                withNoMatch.Groups["Type"].Value,
                noText,
                sortNo);
        }

        var withoutNoMatch = Regex.Match(folderName, @"^(?<Code>\d+)_(?<Type>[^\\/]+)$", RegexOptions.IgnoreCase);
        if (withoutNoMatch.Success && !string.IsNullOrWhiteSpace(withoutNoMatch.Groups["Type"].Value))
        {
            return new FolderIdentity(
                withoutNoMatch.Groups["Code"].Value,
                withoutNoMatch.Groups["Type"].Value.Trim(),
                null,
                null);
        }

        return null;
    }

    private static int GetPreviewStatusSortPriority(string status)
    {
        return status switch
        {
            StatusImportable => 0,
            StatusUpdate => 1,
            StatusCleanup => 2,
            StatusSkipped => 3,
            StatusRuleError => 4,
            StatusDuplicate => 5,
            _ => 5
        };
    }

    public static string NormalizePathKey(string path)
    {
        var normalized = Clean(path).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        while (normalized.Length > 1 && normalized.EndsWith(Path.DirectorySeparatorChar))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    private static string NormalizeNameKey(string name)
    {
        return Clean(name);
    }

    private static bool HasNameConflict(
        IReadOnlyDictionary<string, HashSet<string>> existingPathKeysByName,
        string generatedName,
        string currentPathKey)
    {
        return existingPathKeysByName.TryGetValue(NormalizeNameKey(generatedName), out var pathKeys)
            && pathKeys.Any(pathKey => !string.Equals(pathKey, currentPathKey, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasBatchImportChanges(
        ShortcutItem existingShortcut,
        string generatedName,
        string generatedTargetPath,
        string generatedDescription,
        string generatedSourceModuleName)
    {
        return !string.Equals(Clean(existingShortcut.Name), Clean(generatedName), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Clean(existingShortcut.Description), Clean(generatedDescription), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Clean(existingShortcut.SourceModuleName), Clean(generatedSourceModuleName), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(NormalizePathKey(existingShortcut.TargetPath), NormalizePathKey(generatedTargetPath), StringComparison.OrdinalIgnoreCase);
    }

    private static string Clean(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static ShortcutItem SelectShortcutToKeep(
        IReadOnlyList<ShortcutItem> duplicates,
        string generatedName,
        string generatedTargetPath,
        string generatedDescription,
        string generatedSourceModuleName)
    {
        var matchingShortcuts = duplicates
            .Where(shortcut => !HasBatchImportChanges(shortcut, generatedName, generatedTargetPath, generatedDescription, generatedSourceModuleName))
            .ToList();

        var candidates = matchingShortcuts.Count > 0 ? matchingShortcuts : duplicates;
        return candidates
            .OrderByDescending(shortcut => shortcut.UpdatedAt)
            .First();
    }

    private static int? TryGetRegexNo(Match? regexMatch)
    {
        if (regexMatch is null)
        {
            return null;
        }

        var group = regexMatch.Groups["No"];
        if (!group.Success || !int.TryParse(group.Value, out var no))
        {
            return null;
        }

        return no;
    }

    private static string GenerateName(BatchImportRule rule, string folderName, Match? regexMatch, out string? errorMessage)
    {
        errorMessage = null;
        var result = rule.NameTemplate
            .Replace("{DisplayName}", rule.DisplayName, StringComparison.Ordinal)
            .Replace("{FolderName}", folderName, StringComparison.Ordinal);

        if (string.Equals(rule.MatchType, "Regex", StringComparison.OrdinalIgnoreCase))
        {
            if (regexMatch is null)
            {
                errorMessage = "Regex 规则未提供匹配结果。";
                return result;
            }

            result = ReplaceCaptureGroups(result, regexMatch, out errorMessage);
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                return result;
            }
        }

        var unknownToken = Regex.Match(result, @"\{(?<name>[^{}]+)\}");
        if (unknownToken.Success)
        {
            errorMessage = $"模板引用了不存在或未匹配的捕获组：{unknownToken.Groups["name"].Value}";
        }

        return result;
    }

    private static string ReplaceCaptureGroups(string template, Match match, out string? errorMessage)
    {
        errorMessage = null;
        string? replacementError = null;

        var result = Regex.Replace(template, @"\{(?<name>[^{}]+)\}", tokenMatch =>
        {
            var token = tokenMatch.Groups["name"].Value;

            if (token is "DisplayName" or "FolderName")
            {
                return tokenMatch.Value;
            }

            if (token.StartsWith("Group", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(token["Group".Length..], out var groupIndex))
            {
                if (groupIndex <= 0 || groupIndex >= match.Groups.Count || !match.Groups[groupIndex].Success)
                {
                    replacementError ??= $"模板引用了不存在或未匹配的捕获组：{token}";
                    return string.Empty;
                }

                return match.Groups[groupIndex].Value;
            }

            var groupNames = match.Groups.Keys;
            if (groupNames.Contains(token) && match.Groups[token].Success)
            {
                return match.Groups[token].Value;
            }

            replacementError ??= $"模板引用了不存在或未匹配的捕获组：{token}";
            return string.Empty;
        });

        errorMessage = replacementError;
        return result;
    }

    private static bool TryEnsureModuleName(string targetDirectory, ref string? moduleName, ref string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            return true;
        }

        moduleName = TryReadZamModuleName(targetDirectory, out errorMessage);
        return !string.IsNullOrWhiteSpace(moduleName);
    }

    private static string? TryReadZamModuleName(string targetDirectory, out string? errorMessage)
    {
        errorMessage = null;
        var xmlPath = Path.Combine(targetDirectory, "META-INF", "ZAM-DEPLOY.xml");
        if (!File.Exists(xmlPath))
        {
            errorMessage = "未找到 META-INF\\ZAM-DEPLOY.xml，无法读取模块名。";
            return null;
        }

        try
        {
            var document = XDocument.Load(xmlPath);
            var description = document
                .Root?
                .DescendantsAndSelf()
                .Attributes("description")
                .Select(attribute => attribute.Value)
                .FirstOrDefault(value => value.Contains("Application for ", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(description))
            {
                errorMessage = "ZAM-DEPLOY.xml 中未找到 Application for 模块描述。";
                return null;
            }

            var markerIndex = description.IndexOf("Application for ", StringComparison.OrdinalIgnoreCase);
            var moduleName = description[(markerIndex + "Application for ".Length)..].Trim();
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                errorMessage = "ZAM-DEPLOY.xml 中未找到 Application for 模块描述。";
                return null;
            }

            return moduleName;
        }
        catch (Exception ex)
        {
            errorMessage = $"ZAM-DEPLOY.xml 解析失败：{ex.Message}";
            return null;
        }
    }

    private sealed record RuleMatchResult(BatchImportRule? Rule, Match? RegexMatch, string? ErrorMessage, string Status, string ModuleName);

    private sealed record SimpleModuleMapResult(
        bool Success,
        string GeneratedName,
        string ModuleName,
        int? SortNo,
        int SortRuleIndex,
        string? ErrorMessage,
        string Status)
    {
        public static SimpleModuleMapResult Fail(string status, string errorMessage, int? sortNo = null, string moduleName = "")
        {
            return new SimpleModuleMapResult(false, string.Empty, moduleName, sortNo, int.MaxValue, errorMessage, status);
        }
    }

    private sealed record FolderIdentity(string Code, string Type, string? No, int? SortNo);
}
