using System.Text;
using FirstPinyinWordsHelper = ToolGood.Words.FirstPinyin.WordsHelper;
using FullPinyinWordsHelper = ToolGood.Words.Pinyin.WordsHelper;

namespace VSLoader.Services;

public sealed class ShortcutSearchService
{
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

        try
        {
            var normalizedKeyword = NormalizeForPinyinSearch(keyword);

            return IsInitialsMatch(source, normalizedKeyword)
                || IsFullPinyinMatch(source, normalizedKeyword);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsInitialsMatch(string source, string normalizedKeyword)
    {
        var sourceInitials = NormalizeForPinyinSearch(FirstPinyinWordsHelper.GetFirstPinyin(source));

        return sourceInitials.Length > 0
            && normalizedKeyword.Length > 0
            && sourceInitials.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFullPinyinMatch(string source, string normalizedKeyword)
    {
        var sourcePinyin = NormalizeForPinyinSearch(FullPinyinWordsHelper.GetPinyin(source, false));

        return sourcePinyin.Length > 0
            && normalizedKeyword.Length > 0
            && sourcePinyin.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
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
}
