namespace VSLoader.Tests;

public sealed class ShortcutSearchOptimizationTests
{
    [Fact]
    public void Shortcut_search_skips_pinyin_for_chinese_queries_and_caches_source_indexes()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Models",
            "Services",
            "ShortcutSearchService.cs"));

        Assert.Contains("ConcurrentDictionary<string, SearchIndex>", code);
        Assert.Contains("ContainsCjkCharacter(keyword)", code);
        Assert.Contains("searchIndexCache.GetOrAdd(source, CreateSearchIndex)", code);
    }
}
