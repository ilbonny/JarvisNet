namespace JarvisNet.SearchWeb;

internal static partial class SearchQueryNormalizer
{
    internal static string Normalize(string raw)
    {
        var text = raw.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        text = SttLeadNoisePattern().Replace(text, string.Empty).Trim();
        text = WebSearchLanguage.StripSearchLead(text);

        foreach (var prefix in new[] { "for ", "per ", "pour ", "para ", "um " })
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[prefix.Length..].TrimStart();
                break;
            }
        }

        if (text.StartsWith("to know ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[8..].TrimStart();
        }
        else if (text.StartsWith("sapere ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[7..].TrimStart();
        }

        if (WebSearchLanguage.TryExtractNewsTopic(text, out var newsTopic))
        {
            return newsTopic;
        }

        text = StripLeadingArticles(text);
        return text.TrimEnd('.', '?', '!', '…');
    }

    internal static bool IsNewsQuery(string query) =>
        WebSearchLanguage.ContainsNewsKeyword(query);

    internal static bool PreferTopHeadlinesFeed(string query) =>
        WebSearchLanguage.PreferTopHeadlinesFeed(query);

    private static string StripLeadingArticles(string text)
    {
        ReadOnlySpan<string> prefixes =
        [
            "the ", "a ", "an ", "le ", "la ", "les ", "i ", "gli ", "un ", "una ",
            "el ", "la ", "los ", "las ", "der ", "die ", "das ",
        ];

        foreach (var prefix in prefixes)
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return text[prefix.Length..].TrimStart();
            }
        }

        return text;
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        @"(?i)^(?:"
        + @"(?:i\s+)?(?:was\s+)?(?:searching|looking)\s+(?:on\s+)?(?:the\s+)?(?:internet|web|online)\s*"
        + @"|(?:a\s+me\s+)?(?:cercando|cerca)?\s*(?:su\s+)?(?:internet|online)?\s*(?:ho\s+)?(?:riassunto\s+)?(?:delle?\s+)?"
        + @")\s*",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex SttLeadNoisePattern();
}
