using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class BatchImportServiceSimpleModuleMapTests : IDisposable
{
    private readonly string _rootPath;
    private readonly BatchImportService _service = new();

    public BatchImportServiceSimpleModuleMapTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void LoadRules_reads_simple_module_map_csv()
    {
        var csvPath = Path.Combine(_rootPath, "module-map.csv");
        File.WriteAllText(csvPath, """
ModuleName,DisplayName
eap-sic-Jutze-3D-AOI,矩子3D-AOI
""");

        var rules = _service.LoadRules(csvPath, out var errors);

        Assert.Empty(errors);
        var rule = Assert.Single(rules);
        Assert.Equal("eap-sic-Jutze-3D-AOI", rule.ModuleName);
        Assert.Equal("矩子3D-AOI", rule.DisplayName);
        Assert.Equal("{DisplayName}_{No}", rule.NameTemplate);
    }

    [Fact]
    public void BuildPreview_uses_simple_module_map_and_extracts_no_in_background()
    {
        var folderPath = CreateFolder("12190_TAOI007");
        WriteZamDeployXml(folderPath, """<application description="Application for eap-sic-Jutze-3D-AOI" />""");

        var item = Assert.Single(_service.BuildPreview(_rootPath, CreateSimpleRules(), []));

        Assert.Equal(BatchImportService.StatusImportable, item.Status);
        Assert.Equal("矩子3D-AOI_007", item.GeneratedName);
        Assert.Equal(7, item.SortNo);
    }

    [Fact]
    public void BuildPreview_skips_simple_module_map_when_module_is_not_configured()
    {
        var folderPath = CreateFolder("12190_TAOI007");
        WriteZamDeployXml(folderPath, """<application description="Application for eap-sic-Unknown" />""");

        var item = Assert.Single(_service.BuildPreview(_rootPath, CreateSimpleRules(), []));

        Assert.Equal(BatchImportService.StatusSkipped, item.Status);
        Assert.Contains("模块名未在 CSV 中配置", item.Message);
        Assert.False(item.CanImport);
    }

    [Fact]
    public void BuildPreview_skips_simple_module_map_when_folder_name_has_no_no()
    {
        var folderPath = CreateFolder("InvalidFolder");
        WriteZamDeployXml(folderPath, """<application description="Application for eap-sic-Jutze-3D-AOI" />""");

        var item = Assert.Single(_service.BuildPreview(_rootPath, CreateSimpleRules(), []));

        Assert.Equal(BatchImportService.StatusSkipped, item.Status);
        Assert.Contains("文件夹名无法提取编号 No", item.Message);
        Assert.False(item.CanImport);
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

    private static IReadOnlyList<BatchImportRule> CreateSimpleRules()
    {
        return
        [
            new BatchImportRule
            {
                ModuleName = "eap-sic-Jutze-3D-AOI",
                DisplayName = "矩子3D-AOI",
                NameTemplate = "{DisplayName}_{No}"
            }
        ];
    }
}
