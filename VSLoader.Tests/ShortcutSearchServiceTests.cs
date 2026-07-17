using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ShortcutSearchServiceTests
{
    private readonly ShortcutSearchService _service = new();

    [Theory]
    [InlineData("热贴机_012", "热贴")]
    [InlineData("热贴机_012", "rt")]
    [InlineData("热贴机_012", "rtj")]
    [InlineData("热贴机_012", "rtj012")]
    [InlineData("银烧结_005", "ysj")]
    [InlineData("自动点锡机_012", "zddxj")]
    [InlineData("银烧结NG抓取装置_003", "ysjng")]
    [InlineData("TSSM012", "tssm")]
    public void IsTextMatch_matches_original_text_and_chinese_initials(string source, string keyword)
    {
        Assert.True(_service.IsTextMatch(source, keyword));
    }

    [Theory]
    [InlineData("热贴机_012", "retieji")]
    [InlineData("热贴机_012", "retieji012")]
    [InlineData("热贴机_012", "tieji")]
    [InlineData("银烧结_005", "yinshaojie")]
    [InlineData("自动点锡机_012", "zidong")]
    [InlineData("银烧结NG抓取装置_003", "yinshaojieng")]
    public void IsTextMatch_matches_chinese_full_pinyin(string source, string keyword)
    {
        Assert.True(_service.IsTextMatch(source, keyword));
    }

    [Fact]
    public void IsTextMatch_ignores_common_separators_for_initial_search()
    {
        Assert.True(_service.IsTextMatch("热贴机_012", "rtj012"));
        Assert.True(_service.IsTextMatch("热贴机-012", "rtj012"));
        Assert.True(_service.IsTextMatch("热贴机 012", "rtj012"));
    }

    [Fact]
    public void IsTextMatch_returns_false_for_unrelated_keyword()
    {
        Assert.False(_service.IsTextMatch("热贴机_012", "ysj"));
    }

    [Fact]
    public void IsTextMatch_skips_pinyin_index_for_unmatched_chinese_keyword()
    {
        Assert.False(_service.IsTextMatch("热贴机_012", "完全不存在"));
    }
}
