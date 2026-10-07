using System.Text.RegularExpressions;

namespace JarvisNet.SearchWeb;

internal static partial class BrowserResponseGuard
{
    /// <summary>Istruzione neutra (inglese): i modelli multilingue la seguono bene.</summary>
    internal const string ToolNudgeUserMessage =
        "Use the registered browser tools now to navigate or read the page. "
        + "Do not invent hotels, prices, ratings, or result lists—only report what you see after using tools.";

    internal static bool LikelyWebUserIntent(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return false;
        }

        if (InternetSearchAssist.IsInternetSearchRequest(userMessage))
        {
            return false;
        }

        var text = userMessage.AsSpan();
        if (ContainsUrlOrDomain(text))
        {
            return true;
        }

        var lower = userMessage.ToLowerInvariant();
        ReadOnlySpan<string> browserHints =
        [
            "browser", "webpage", "web page", "website", "web site", "snapshot",
            "navigate", "navigation", "click", "booking.com", "amazon.",
            "playwright", "http://", "https://", "www.",
            // lessici comuni (non esclusivi)
            "open ", "visit ", "goto ", "go to ",
            "tab ", "page ",
        ];

        foreach (var hint in browserHints)
        {
            if (lower.Contains(hint, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return BrowserActionVerbPattern().IsMatch(lower);
    }

    internal static bool LooksLikeFabricatedBrowserAnswer(string assistantText)
    {
        if (string.IsNullOrWhiteSpace(assistantText))
        {
            return false;
        }

        if (HasPlaceholderOrTemplateMarkers(assistantText))
        {
            return true;
        }

        if (HasMarkdownResultListPattern(assistantText))
        {
            return true;
        }

        if (HasPriceOrRatingPattern(assistantText))
        {
            return true;
        }

        var lower = assistantText.ToLowerInvariant();
        if (FabricatedCompletionPattern().IsMatch(lower))
        {
            return true;
        }

        return RefusalInsteadOfToolPattern().IsMatch(lower);
    }

    internal static bool ShouldRetryWithoutTools(string userMessage, string assistantText, int toolsInvoked) =>
        toolsInvoked == 0
        && LikelyWebUserIntent(userMessage)
        && LooksLikeFabricatedBrowserAnswer(assistantText);

    private static bool ContainsUrlOrDomain(ReadOnlySpan<char> text) =>
        UrlLikePattern().IsMatch(text);

    private static bool HasPlaceholderOrTemplateMarkers(string text)
    {
        if (BracketPlaceholderPattern().IsMatch(text))
        {
            return true;
        }

        return text.Contains("[details]", StringComparison.OrdinalIgnoreCase)
            || text.Contains("[dettagli]", StringComparison.OrdinalIgnoreCase)
            || text.Contains("lorem ipsum", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMarkdownResultListPattern(string text)
    {
        if (MarkdownBoldListItemPattern().IsMatch(text))
        {
            return true;
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var listLikeLines = 0;
        foreach (var line in lines)
        {
            if (line.StartsWith('-') || line.StartsWith('*') || NumberedListLinePattern().IsMatch(line))
            {
                listLikeLines++;
            }
        }

        return listLikeLines >= 2;
    }

    private static bool HasPriceOrRatingPattern(string text)
    {
        if (CurrencyWithAmountPattern().IsMatch(text))
        {
            return true;
        }

        return RatingPattern().IsMatch(text);
    }

    [GeneratedRegex(@"(https?://|www\.)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlLikePattern();

    [GeneratedRegex(
        @"\b(open(ed)?|visit(ed)?|navigate(d)?|browse(d)?|click(ed)?|go to|goto)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BrowserActionVerbPattern();

    [GeneratedRegex(@"\[[^\]]{3,}\]", RegexOptions.CultureInvariant)]
    private static partial Regex BracketPlaceholderPattern();

    [GeneratedRegex(@"^\s*[-*]\s+\*\*", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownBoldListItemPattern();

    [GeneratedRegex(@"^\s*\d+[.)]\s+", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex NumberedListLinePattern();

    [GeneratedRegex(@"[$€£¥]\s*\d|\d\s*[$€£¥]", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyWithAmountPattern();

    [GeneratedRegex(
        @"\b\d([,.]\d+)?\s*(/10|/5|stars?|★|⭐)\b|\b(rating|score|punteggio|valutazione)\s*:?\s*\d",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RatingPattern();

    [GeneratedRegex(
        @"\b("
        + @"browser\s+(is|'s)\s+(now\s+)?open(ed)?"
        + @"|site\s+(is|'s)\s+open(ed)?"
        + @"|here\s+are\s+the\s+results"
        + @"|search\s+(was|has\s+been)\s+(completed|done|performed)"
        + @"|results\s*:"
        + @"|ecco\s+i\s+risultati"
        + @"|(?:è|e)\s+(?:ora\s+)?(?:aperto|aperta|stato\s+aperto)"
        + @"|est[aá]\s+abierto"
        + @"|est\s+ouvert"
        + @"|wurde\s+ge(?:öffnet|offnet)"
        + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FabricatedCompletionPattern();

    [GeneratedRegex(
        @"\b(i\s+can'?t|cannot|unable\s+to)\s+(browse|navigate|access)"
        + @"|non\s+posso\s+(navigare|accedere)"
        + @"|no\s+puedo\s+(navegar|acceder)"
        + @"|je\s+ne\s+peux\s+pas\s+(naviguer|accéder)"
        + @"|ich\s+kann\s+nicht\s+(surfen|navigieren|zugreifen)"
        + @"|\b(i\s+will|let\s+me)\s+guide\s+you\b"
        + @"|simulate\s+(these\s+)?steps",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RefusalInsteadOfToolPattern();
}
