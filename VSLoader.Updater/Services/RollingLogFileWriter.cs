using System.IO;

namespace VSLoader.Updater.Services;

internal static class RollingLogFileWriter
{
    public const int DefaultMaxLines = 2000;

    private static readonly object SyncRoot = new();

    public static void Append(string filePath, string content, int maxLines = DefaultMaxLines)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrEmpty(content))
        {
            return;
        }

        lock (SyncRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var existingLines = File.Exists(filePath)
                ? File.ReadLines(filePath).ToList()
                : [];
            var newLines = SplitLines(content);
            var lines = existingLines.Concat(newLines)
                .TakeLast(Math.Max(1, maxLines))
                .ToArray();

            File.WriteAllLines(filePath, lines);
        }
    }

    private static IEnumerable<string> SplitLines(string content)
    {
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }
}
