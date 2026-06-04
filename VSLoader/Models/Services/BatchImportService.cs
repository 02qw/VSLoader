using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
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
            if (!RequiredHeaders.All(required => headers.Contains(required)))
            {
                errors.Add("CSV 表头不正确，必须是 MatchType,Pattern,DisplayName,NameTemplate。");
                return validRules;
            }

            var rowNumber = 1;
            while (csv.Read())
            {
                rowNumber++;
                var rule = new BatchImportRule
                {
                    MatchType = csv.GetField(nameof(BatchImportRule.MatchType))?.Trim() ?? string.Empty,
                    Pattern = csv.GetField(nameof(BatchImportRule.Pattern))?.Trim() ?? string.Empty,
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
        IEnumerable<string>? ruleErrors = null)
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
        var previewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previewPathKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in Directory.EnumerateDirectories(parentFolderPath))
        {
            var folderName = Path.GetFileName(directory);
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
                continue;
            }

            var matchResult = FindMatchingRule(rules, folderName);

            if (matchResult.Rule is null)
            {
                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    Status = StatusSkipped,
                    Message = "未匹配任何规则。",
                    CanImport = false,
                    IsSelected = false,
                    SortRuleIndex = int.MaxValue - 1,
                    SortName = folderName
                });
                continue;
            }

            var generatedName = GenerateName(matchResult.Rule, folderName, matchResult.RegexMatch, out var nameError).Trim();
            var generatedDescription = $"批量新增：{folderName}";
            var sortNo = TryGetRegexNo(matchResult.RegexMatch);
            if (!string.IsNullOrWhiteSpace(nameError))
            {
                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    GeneratedName = generatedName,
                    MatchedPattern = matchResult.Rule.Pattern,
                    Status = StatusRuleError,
                    Message = nameError,
                    CanImport = false,
                    IsSelected = false,
                    SortRuleIndex = int.MaxValue,
                    SortNo = sortNo,
                    SortName = string.IsNullOrWhiteSpace(generatedName) ? folderName : generatedName
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(generatedName))
            {
                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    MatchedPattern = matchResult.Rule.Pattern,
                    Status = StatusRuleError,
                    Message = "名称模板生成了空名称。",
                    CanImport = false,
                    IsSelected = false,
                    SortRuleIndex = int.MaxValue,
                    SortNo = sortNo,
                    SortName = folderName
                });
                continue;
            }

            var existingGroupForPath = existingGroupsByPath.TryGetValue(pathKey, out var matchedGroup)
                ? matchedGroup
                : [];
            var existingShortcutForPath = existingGroupForPath.Count > 0
                ? existingGroupForPath[0]
                : null;
            var hasNameConflict = HasNameConflict(existingList, generatedName, pathKey)
                || !previewNames.Add(generatedName);
            if (hasNameConflict)
            {
                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    GeneratedName = generatedName,
                    MatchedPattern = matchResult.Rule.Pattern,
                    Status = StatusDuplicate,
                    Message = "生成名称与已有快捷项或本次预览项目重复。",
                    CanImport = false,
                    IsSelected = false,
                    SortRuleIndex = matchResult.Rule.SortIndex,
                    SortNo = sortNo,
                    SortName = generatedName
                });
                continue;
            }

            if (existingGroupForPath.Count > 1)
            {
                var keepShortcut = SelectShortcutToKeep(existingGroupForPath, generatedName, directory, generatedDescription);
                var duplicateShortcutsToRemove = existingGroupForPath
                    .Where(shortcut => !ReferenceEquals(shortcut, keepShortcut))
                    .ToList();

                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    GeneratedName = generatedName,
                    MatchedPattern = matchResult.Rule.Pattern,
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
                    SortRuleIndex = matchResult.Rule.SortIndex,
                    SortNo = sortNo,
                    SortName = generatedName
                });
                continue;
            }

            if (existingShortcutForPath is not null)
            {
                if (!HasBatchImportChanges(existingShortcutForPath, generatedName, directory, generatedDescription))
                {
                    items.Add(new BatchImportPreviewItem
                    {
                        FolderName = folderName,
                        TargetPath = directory,
                        GeneratedName = generatedName,
                        MatchedPattern = matchResult.Rule.Pattern,
                        ExistingTargetPath = existingShortcutForPath.TargetPath,
                        ExistingName = existingShortcutForPath.Name,
                        ExistingShortcutToUpdate = existingShortcutForPath,
                        Status = StatusSkipped,
                        Message = "目标路径已存在，且当前规则结果无变化。",
                        CanImport = false,
                        IsSelected = false,
                        IsUpdate = false,
                        SortRuleIndex = matchResult.Rule.SortIndex,
                        SortNo = sortNo,
                        SortName = generatedName
                    });
                    continue;
                }

                items.Add(new BatchImportPreviewItem
                {
                    FolderName = folderName,
                    TargetPath = directory,
                    GeneratedName = generatedName,
                    MatchedPattern = matchResult.Rule.Pattern,
                    ExistingTargetPath = existingShortcutForPath.TargetPath,
                    ExistingName = existingShortcutForPath.Name,
                    ExistingShortcutToUpdate = existingShortcutForPath,
                    Status = StatusUpdate,
                    Message = $"目标路径已存在，将更新：{existingShortcutForPath.Name} -> {generatedName}",
                    CanImport = true,
                    IsSelected = true,
                    IsUpdate = true,
                    SortRuleIndex = matchResult.Rule.SortIndex,
                    SortNo = sortNo,
                    SortName = generatedName
                });
                continue;
            }

            items.Add(new BatchImportPreviewItem
            {
                FolderName = folderName,
                TargetPath = directory,
                GeneratedName = generatedName,
                MatchedPattern = matchResult.Rule.Pattern,
                Status = StatusImportable,
                Message = "可新增。",
                CanImport = true,
                IsSelected = true,
                SortRuleIndex = matchResult.Rule.SortIndex,
                SortNo = sortNo,
                SortName = generatedName
            });
        }

        return items
            .OrderBy(item => GetPreviewStatusSortPriority(item.Status))
            .ThenBy(item => item.SortRuleIndex)
            .ThenBy(item => item.SortNo ?? int.MaxValue)
            .ThenBy(item => item.SortName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
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
                    CreatedAt = now,
                    UpdatedAt = now
                }
            })
            .ToList();
    }

    private static List<string> ValidateRule(BatchImportRule rule, int rowNumber)
    {
        var errors = new List<string>();

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

        return errors;
    }

    private static RuleMatchResult FindMatchingRule(IEnumerable<BatchImportRule> rules, string folderName)
    {
        foreach (var rule in rules)
        {
            if (string.Equals(rule.MatchType, "Contains", StringComparison.OrdinalIgnoreCase)
                && folderName.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                return new RuleMatchResult(rule, null);
            }

            if (string.Equals(rule.MatchType, "Regex", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(folderName, rule.Pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return new RuleMatchResult(rule, match);
                }
            }
        }

        return new RuleMatchResult(null, null);
    }

    private static bool IsSupportedMatchType(string matchType)
    {
        return string.Equals(matchType, "Contains", StringComparison.OrdinalIgnoreCase)
            || string.Equals(matchType, "Regex", StringComparison.OrdinalIgnoreCase);
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
        var normalized = path.Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        while (normalized.Length > 1 && normalized.EndsWith(Path.DirectorySeparatorChar))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    private static bool HasNameConflict(IEnumerable<ShortcutItem> existingShortcuts, string generatedName, string currentPathKey)
    {
        return existingShortcuts.Any(shortcut =>
            string.Equals(shortcut.Name.Trim(), generatedName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(NormalizePathKey(shortcut.TargetPath), currentPathKey, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasBatchImportChanges(
        ShortcutItem existingShortcut,
        string generatedName,
        string generatedTargetPath,
        string generatedDescription)
    {
        return !string.Equals(existingShortcut.Name.Trim(), generatedName.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existingShortcut.Description.Trim(), generatedDescription.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(NormalizePathKey(existingShortcut.TargetPath), NormalizePathKey(generatedTargetPath), StringComparison.OrdinalIgnoreCase);
    }

    private static ShortcutItem SelectShortcutToKeep(
        IReadOnlyList<ShortcutItem> duplicates,
        string generatedName,
        string generatedTargetPath,
        string generatedDescription)
    {
        var matchingShortcuts = duplicates
            .Where(shortcut => !HasBatchImportChanges(shortcut, generatedName, generatedTargetPath, generatedDescription))
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

    private sealed record RuleMatchResult(BatchImportRule? Rule, Match? RegexMatch);
}
