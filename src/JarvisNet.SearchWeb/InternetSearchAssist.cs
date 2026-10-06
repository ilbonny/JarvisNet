using System.Text.RegularExpressions;

namespace JarvisNet.SearchWeb;

internal static partial class InternetSearchAssist
{
    internal const string SearchToolNudgeUserMessage =
        "Usa subito il tool SearchWeb-search_internet con una query concreta derivata dalla richiesta dell'utente. "
        + "Non aprire il browser MCP per una semplice ricerca web.";

    internal static bool IsInternetSearchRequest(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return false;
        }

        var text = userMessage.ToLowerInvariant();
        if (text.Contains("cerca su internet", StringComparison.Ordinal)
            || text.Contains("cerca in internet", StringComparison.Ordinal)
            || text.Contains("ricerca su internet", StringComparison.Ordinal)
            || text.Contains("ricerca in internet", StringComparison.Ordinal)
            || text.Contains("cerca online", StringComparison.Ordinal)
            || text.Contains("ricerca online", StringComparison.Ordinal)
            || text.Contains("cerca sul web", StringComparison.Ordinal)
            || text.Contains("ricerca sul web", StringComparison.Ordinal))
        {
            return true;
        }

        if (InternetSearchPattern().IsMatch(text))
        {
            return true;
        }

        return text.Contains("notizie", StringComparison.Ordinal)
            && (text.Contains("oggi", StringComparison.Ordinal)
                || text.Contains("internet", StringComparison.Ordinal)
                || text.Contains("online", StringComparison.Ordinal)
                || text.Contains("cerca", StringComparison.Ordinal)
                || text.Contains("ricerca", StringComparison.Ordinal));
    }

    internal static bool TryBuildSearchQuery(string userMessage, out string query)
    {
        query = string.Empty;
        if (!IsInternetSearchRequest(userMessage))
        {
            return false;
        }

        var text = userMessage.Trim();
        text = SearchLeadPattern().Replace(text, string.Empty).Trim();
        text = text.TrimStart('.', ',', ':', '-', '—', ' ');

        if (text.StartsWith("per ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[4..].Trim();
        }

        if (text.StartsWith("del ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("dei ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("della ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("delle ", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsInternetSearchRequest(text))
            {
                return false;
            }
        }

        text = text.TrimEnd('.', '?', '!', '…');

        if (TryExtractNewsOnlyQuery(userMessage, text, out query))
        {
            return true;
        }

        if (text.Length < 2
            || IsOnlySearchFiller(text)
            || GenericSearchOnlyPattern().IsMatch(text))
        {
            return false;
        }

        query = text;
        return true;
    }

    private static bool TryExtractNewsOnlyQuery(string userMessage, string strippedText, out string query)
    {
        query = string.Empty;
        var source = strippedText;
        if (string.IsNullOrWhiteSpace(source))
        {
            source = userMessage;
        }

        var match = NotizieRelativePattern().Match(source);
        if (match.Success)
        {
            query = $"notizie {match.Groups[1].Value.Trim()}";
            return true;
        }

        match = NotizieDirectPattern().Match(source);
        if (match.Success)
        {
            query = match.Groups[1].Value.Trim();
            return query.Length >= 3;
        }

        return false;
    }

    private static bool IsOnlySearchFiller(string text)
    {
        ReadOnlySpan<string> fillers =
        [
            "ora", "adesso", "solo", "qualcosa", "qualche cosa", "cosa", "un argomento", "un topic",
        ];

        var normalized = text.Trim().ToLowerInvariant();
        foreach (var filler in fillers)
        {
            if (normalized.Equals(filler, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool ShouldRunDirectSearch(string userMessage, int toolsInvoked) =>
        toolsInvoked == 0
        && TryBuildSearchQuery(userMessage, out _);

    internal static bool ShouldNudgeSearchTool(string userMessage, string assistantText, int toolsInvoked) =>
        toolsInvoked == 0
        && IsInternetSearchRequest(userMessage)
        && TryBuildSearchQuery(userMessage, out _)
        && string.IsNullOrWhiteSpace(assistantText);

    [GeneratedRegex(@"(cerca|ricerca|cercare).{0,24}(internet|online|web|rete)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InternetSearchPattern();

    [GeneratedRegex(
        @"(?i)^(?:\s*(?:allora|ora|adesso|per favore|puoi|potresti|vorrei che)\s+)*"
        + @"(?:fai\s+)?(?:una\s+)?(?:ricerca|cerca(?:re)?)\s+"
        + @"(?:su\s+|in\s+|sul\s+|sull['’]?\s*|nell['’]?\s*)?"
        + @"(?:internet|web|online|rete)\s*"
        + @"(?:per\s+(?:vedere|sapere|conoscere|capire|controllare))?\s*"
        + @"(?:quali\s+sono\s+(?:le\s+|i\s+|gli\s+)?)?",
        RegexOptions.CultureInvariant)]
    private static partial Regex SearchLeadPattern();

    [GeneratedRegex(
        @"(?i)^(?:ora\s+|adesso\s+)?(?:fai\s+)?(?:una\s+)?(?:ricerca|cerca(?:re)?)\s+(?:su\s+|in\s+|sul\s+)?(?:internet|web|online|rete)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex GenericSearchOnlyPattern();

    [GeneratedRegex(@"(?i)(?:le\s+)?notizie\s+relative\s+(?:alle?\s+|ai\s+)(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex NotizieRelativePattern();

    [GeneratedRegex(
        @"(?i)^(?:le\s+)?((?:ultime\s+)?notizie(?:\s+(?:di\s+)?oggi|\s+attuali|\s+principali)?(?:\s+.+)?)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex NotizieDirectPattern();
}
