using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class BatchImportServiceZamModuleTests : IDisposable
{
    private readonly string _rootPath;
    private readonly BatchImportService _service = new();

    public BatchImportServiceZamModuleTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void BuildPreview_uses_zam_module_pattern_to_replace_display_name_only()
    {
        var folderPath = CreateFolder("12190_TAOI007");
        WriteZamDeployXml(folderPath, """<application description="Application for eap-sic-Jutze-3D-AOI" />""");

        var item = Assert.Single(_service.BuildPreview(_rootPath, CreateTaoiZamRules(), []));

        Assert.Equal(BatchImportService.StatusImportable, item.Status);
        Assert.Equal("3D-AOI_007", item.GeneratedName);
        Assert.Equal(7, item.SortNo);
    }

    [Fact]
    public void BuildPreview_skips_new_rule_when_zam_deploy_xml_is_missing()
    {
        _ = CreateFolder("12190_TAOI007");

        var item = Assert.Single(_service.BuildPreview(_rootPath, CreateTaoiZamRules(), []));

        Assert.Equal(BatchImportService.StatusSkipped, item.Status);
        Assert.Contains("未找到 META-INF", item.Message);
        Assert.False(item.CanImport);
    }

    [Fact]
    public void BuildPreview_marks_invalid_zam_deploy_xml_as_rule_error()
    {
        var folderPath = CreateFolder("12190_TAOI007");
        WriteZamDeployXml(folderPath, "<application");

        var item = Assert.Single(_service.BuildPreview(_rootPath, CreateTaoiZamRules(), []));

        Assert.Equal(BatchImportService.StatusRuleError, item.Status);
        Assert.Contains("ZAM-DEPLOY.xml 解析失败", item.Message);
        Assert.False(item.CanImport);
    }

    [Fact]
    public void BuildPreview_keeps_legacy_rules_working_without_module_pattern()
    {
        _ = CreateFolder("12190_TAOI007");
        var rules = new[]
        {
            new BatchImportRule
            {
                MatchType = "Regex",
                Pattern = @"^(?<Code>\d+)_(?<Type>TAOI)(?<No>\d+)$",
                DisplayName = "旧3D",
                NameTemplate = "{DisplayName}_{No}"
            }
        };

        var item = Assert.Single(_service.BuildPreview(_rootPath, rules, []));

        Assert.Equal(BatchImportService.StatusImportable, item.Status);
        Assert.Equal("旧3D_007", item.GeneratedName);
    }

    [Fact]
    public void LoadRules_reads_optional_module_pattern()
    {
        var csvPath = Path.Combine(_rootPath, "rules.csv");
        File.WriteAllText(csvPath, """
MatchType,Pattern,ModulePattern,DisplayName,NameTemplate
Regex,^(?<Code>\d+)_(?<Type>TAOI)(?<No>\d+)$,^eap-sic-Jutze-3D-AOI$,3D-AOI,{DisplayName}_{No}
""");

        var rules = _service.LoadRules(csvPath, out var errors);

        Assert.Empty(errors);
        var rule = Assert.Single(rules);
        Assert.Equal("^eap-sic-Jutze-3D-AOI$", rule.ModulePattern);
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

    private static void WriteZamDeployXml(string folderPath, string content)
    {
        var metaInfPath = Path.Combine(folderPath, "META-INF");
        Directory.CreateDirectory(metaInfPath);
        File.WriteAllText(Path.Combine(metaInfPath, "ZAM-DEPLOY.xml"), content);
    }

    private static IReadOnlyList<BatchImportRule> CreateTaoiZamRules()
    {
        return
        [
            new BatchImportRule
            {
                MatchType = "Regex",
                Pattern = @"^(?<Code>\d+)_(?<Type>TAOI)(?<No>\d+)$",
                ModulePattern = "^eap-sic-Jutze-3D-AOI$",
                DisplayName = "3D-AOI",
                NameTemplate = "{DisplayName}_{No}"
            }
        ];
    }
}
