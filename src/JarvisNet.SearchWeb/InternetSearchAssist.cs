namespace JarvisNet.SearchWeb;

internal static class InternetSearchAssist
{
    internal const string SearchToolNudgeUserMessage =
        "Use the SearchWeb-search_internet tool immediately with a concrete query derived from the user request. "
        + "Do not open the MCP browser for a simple web search.";

    internal static bool IsInternetSearchRequest(string userMessage) =>
        WebSearchLanguage.IsInternetSearchRequest(userMessage);

    internal static bool TryBuildSearchQuery(string userMessage, out string query)
    {
        query = string.Empty;
        if (!IsInternetSearchRequest(userMessage))
        {
            return false;
        }

        var text = userMessage.Trim();
        text = WebSearchLanguage.StripSearchLead(text);
        text = text.TrimStart('.', ',', ':', '-', '—', ' ');

        if (TryStripLeadingPreposition(ref text))
        {
            // stripped "for / per / pour / para / um"
        }

        text = text.TrimEnd('.', '?', '!', '…');

        if (TryExtractNewsOnlyQuery(userMessage, text, out query))
        {
            return true;
        }

        if (text.Length < 2
            || WebSearchLanguage.IsSearchFillerOnly(text)
            || WebSearchLanguage.IsGenericSearchOnlyRequest(text)
            || WebSearchLanguage.IsGenericSearchOnlyRequest(userMessage.Trim()))
        {
            return false;
        }

        query = text;
        return true;
    }

    private static bool TryStripLeadingPreposition(ref string text)
    {
        ReadOnlySpan<string> prefixes =
        [
            "for ", "to ", "about ", "per ", "pour ", "para ", "um ", "über ", "sur ",
        ];

        foreach (var prefix in prefixes)
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[prefix.Length..].TrimStart();
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractNewsOnlyQuery(string userMessage, string strippedText, out string query)
    {
        query = string.Empty;
        var source = string.IsNullOrWhiteSpace(strippedText) ? userMessage : strippedText;

        if (WebSearchLanguage.TryExtractNewsTopic(source, out query))
        {
            return true;
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
}
