using System.Text.RegularExpressions;

namespace JarvisNet.Engine;

public static partial class TtsSpeechFormatter
{
    public static string SanitizeForSpeech(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var result = text.Trim();

        result = FencedCodeBlockRegex().Replace(result, " ");
        result = InlineCodeRegex().Replace(result, "$1");
        result = MarkdownLinkRegex().Replace(result, "$1");
        result = RawUrlRegex().Replace(result, " ");
        result = BoldItalicRegex().Replace(result, "$1");
        result = ListMarkerRegex().Replace(result, " ");
        result = HeadingRegex().Replace(result, "$1");
        result = NonSpeechSymbolRegex().Replace(result, " ");
        result = EmojiRegex().Replace(result, " ");
        result = WhitespaceRegex().Replace(result, " ").Trim();

        return result;
    }

    [GeneratedRegex("```[\\s\\S]*?```", RegexOptions.Multiline)]
    private static partial Regex FencedCodeBlockRegex();

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex("\\[([^\\]]+)\\]\\([^\\)]+\\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex("https?://\\S+", RegexOptions.IgnoreCase)]
    private static partial Regex RawUrlRegex();

    [GeneratedRegex("\\*\\*([^*]+)\\*\\*|__([^_]+)__|\\*([^*]+)\\*|_([^_]+)_")]
    private static partial Regex BoldItalicRegex();

    [GeneratedRegex("^[\\-\\*\\+]\\s+", RegexOptions.Multiline)]
    private static partial Regex ListMarkerRegex();

    [GeneratedRegex("^#{1,6}\\s+", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex("[\\*#`_~>|\\[\\]{}]", RegexOptions.None)]
    private static partial Regex NonSpeechSymbolRegex();

    [GeneratedRegex("[\\u2600-\\u27BF\\uD83C-\\uDBFF\\uDC00-\\uDFFF]+")]
    private static partial Regex EmojiRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
