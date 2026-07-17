using System.Collections.Concurrent;
using System.Text;
using FirstPinyinWordsHelper = ToolGood.Words.FirstPinyin.WordsHelper;
using FullPinyinWordsHelper = ToolGood.Words.Pinyin.WordsHelper;

namespace VSLoader.Services;

public sealed class ShortcutSearchService
{
    private readonly ConcurrentDictionary<string, SearchIndex> searchIndexCache = new(StringComparer.Ordinal);

    public bool IsTextMatch(string source, string keyword)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        if (source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedKeyword = NormalizeForPinyinSearch(keyword);
        if (normalizedKeyword.Length == 0 || ContainsCjkCharacter(keyword))
        {
            return false;
        }

        try
        {
            var searchIndex = searchIndexCache.GetOrAdd(source, CreateSearchIndex);

            return searchIndex.Initials.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                || searchIndex.FullPinyin.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static SearchIndex CreateSearchIndex(string source)
    {
        return new SearchIndex(
            NormalizeForPinyinSearch(FirstPinyinWordsHelper.GetFirstPinyin(source)),
            NormalizeForPinyinSearch(FullPinyinWordsHelper.GetPinyin(source, false)));
    }

    private static bool ContainsCjkCharacter(string value)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            var codePoint = rune.Value;
            if ((codePoint >= 0x3400 && codePoint <= 0x4DBF)
                || (codePoint >= 0x4E00 && codePoint <= 0x9FFF)
                || (codePoint >= 0xF900 && codePoint <= 0xFAFF)
                || (codePoint >= 0x20000 && codePoint <= 0x3134F)
                || (codePoint >= 0x2F800 && codePoint <= 0x2FA1F))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeForPinyinSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is ' ' or '_' or '-' or '.')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private sealed record SearchIndex(string Initials, string FullPinyin);
}
