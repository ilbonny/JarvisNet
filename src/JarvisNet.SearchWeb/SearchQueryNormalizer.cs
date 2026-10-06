using System.Text.RegularExpressions;

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
        text = SearchLeadPattern().Replace(text, string.Empty).Trim();

        if (text.StartsWith("per ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[4..].Trim();
        }

        if (text.StartsWith("sapere ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[7..].Trim();
        }

        if (TryExtractNewsTopic(text, out var newsTopic))
        {
            return newsTopic;
        }

        text = NotizieRelativePattern().Replace(text, string.Empty).Trim();
        foreach (var prefix in new[] { "le ", "i ", "gli ", "un ", "una " })
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[prefix.Length..].TrimStart();
            }
        }

        return text.TrimEnd('.', '?', '!', '…');
    }

    internal static bool IsNewsQuery(string query)
    {
        var t = query.ToLowerInvariant();
        return t.Contains("notizie", StringComparison.Ordinal)
            || t.Contains("notizia", StringComparison.Ordinal)
            || t.Contains("titoli", StringComparison.Ordinal)
            || t.Contains("attualità", StringComparison.Ordinal)
            || t.Contains("attualita", StringComparison.Ordinal);
    }

    internal static bool PreferTopHeadlinesFeed(string query)
    {
        var t = query.ToLowerInvariant();
        if (!IsNewsQuery(t))
        {
            return false;
        }

        if (TopHeadlinesPattern().IsMatch(t))
        {
            return true;
        }

        // Solo "notizie" + aggettivo generico, senza argomento specifico.
        return t is "notizie" or "notizie attuali" or "notizie di oggi" or "notizie oggi"
            or "ultime notizie" or "notizie principali";
    }

    private static bool TryExtractNewsTopic(string text, out string topic)
    {
        topic = string.Empty;
        var match = NotizieRelativePattern().Match(text);
        if (match.Success && match.Groups[1].Value.Length > 2)
        {
            topic = $"notizie {match.Groups[1].Value.Trim()}";
            return true;
        }

        match = NotizieClausePattern().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var clause = match.Groups[1].Value.Trim();
        if (clause.Length < 3)
        {
            return false;
        }

        topic = clause;
        return true;
    }

    [GeneratedRegex(
        @"(?i)^(?:a me\s+)?(?:cercando|cerca)?\s*(?:su\s+)?(?:internet|online)?\s*(?:ho\s+)?(?:riassunto\s+)?(?:delle?\s+)?",
        RegexOptions.CultureInvariant)]
    private static partial Regex SttLeadNoisePattern();

    [GeneratedRegex(
        @"(?i)^(?:fai\s+)?(?:una\s+)?(?:ricerca|cerca(?:re)?)\s+"
        + @"(?:su\s+|in\s+|sul\s+)?(?:internet|web|online)\s*"
        + @"(?:per\s+(?:sapere|vedere|conoscere))?\s*",
        RegexOptions.CultureInvariant)]
    private static partial Regex SearchLeadPattern();

    [GeneratedRegex(@"(?i)^(?:le\s+)?notizie\s+relative\s+(?:alle?\s+|ai\s+)(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex NotizieRelativePattern();

    [GeneratedRegex(
        @"(?i)(notizie(?:\s+(?:di\s+)?oggi|\s+attuali|\s+principali|\s+ultime)?(?:\s+.+)?)",
        RegexOptions.CultureInvariant)]
    private static partial Regex NotizieClausePattern();

    [GeneratedRegex(
        @"(?i)^(?:le\s+)?(?:ultime\s+)?notizie(?:\s+(?:di\s+)?oggi|\s+attuali|\s+principali)?\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TopHeadlinesPattern();
}
