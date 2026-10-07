using System.Text.RegularExpressions;

namespace JarvisNet.SearchWeb;

/// <summary>Pattern multilingue condivisi per intent ricerca web e notizie.</summary>
internal static partial class WebSearchLanguage
{
    internal static bool ContainsNewsKeyword(string text)
    {
        var t = text.ToLowerInvariant();
        return NewsKeywordPattern().IsMatch(t);
    }

    internal static bool IsInternetSearchRequest(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return false;
        }

        var text = userMessage.ToLowerInvariant();
        if (ExplicitWebSearchPhrasePattern().IsMatch(text))
        {
            return true;
        }

        if (SearchNearWebPattern().IsMatch(text))
        {
            return true;
        }

        return NewsSearchIntentPattern().IsMatch(text);
    }

    internal static bool IsGenericSearchOnlyRequest(string text) =>
        GenericSearchOnlyPattern().IsMatch(text.Trim());

    internal static bool IsSearchFillerOnly(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        return FillerOnlyPattern().IsMatch(normalized);
    }

    internal static string StripSearchLead(string text) =>
        MultilingualSearchLeadPattern().Replace(text, string.Empty).Trim();

    internal static bool TryExtractNewsTopic(string text, out string topic)
    {
        topic = string.Empty;
        var match = NewsRelativePattern().Match(text);
        if (match.Success && match.Groups[1].Value.Trim().Length > 2)
        {
            topic = "news " + match.Groups[1].Value.Trim();
            return true;
        }

        match = NewsClausePattern().Match(text);
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

    internal static bool PreferTopHeadlinesFeed(string query)
    {
        var t = query.Trim().ToLowerInvariant();
        if (!ContainsNewsKeyword(t))
        {
            return false;
        }

        return TopHeadlinesOnlyPattern().IsMatch(t);
    }

    [GeneratedRegex(
        @"\b("
        + @"search\s+(the\s+)?(web|internet|online)|web\s+search|internet\s+search|look\s+up\s+online|google\s+search"
        + @"|cerca\s+(su|in)\s+(internet|web|online)|ricerca\s+(su|in)\s+(internet|web|online)"
        + @"|busca(r)?\s+(en\s+)?(internet|la\s+web|online)|búsqueda\s+(en\s+)?(internet|web)"
        + @"|recherche\s+(sur\s+)?(internet|le\s+web|en\s+ligne)"
        + @"|suche\s+(im\s+)?(internet|web|online)"
        + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitWebSearchPhrasePattern();

    [GeneratedRegex(
        @"\b(search|buscar|busca|chercher|cherche|cerca|cercare|ricerca|suche|suchen)\b.{0,28}\b(internet|online|web|rete|net)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SearchNearWebPattern();

    [GeneratedRegex(
        @"\b(news|headlines|breaking\s+news|notizie|noticia|noticias|nouvelles|nachrichten|titoli)\b"
        + @".{0,20}\b(today|oggi|hoy|aujourd'hui|jetzt|now|current|latest|attual(?:i|ità)?|actuales|actuelles)\b"
        + @"|\b(today'?s?\s+news|notizie\s+(di\s+)?oggi|noticias\s+de\s+hoy)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NewsSearchIntentPattern();

    [GeneratedRegex(
        @"\b(news|headlines|notizie|noticia|noticias|nouvelles|nachrichten|titoli|headline)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NewsKeywordPattern();

    [GeneratedRegex(
        @"(?i)^(?:\s*(?:please|ok|well|so|then|now|allora|ora|adesso|per\s+favore|por\s+favor|bitte)\s+)*"
        + @"(?:(?:do\s+a|run\s+a|perform\s+a|make\s+a)\s+)?(?:web\s+|internet\s+)?(?:search|lookup)\s+"
        + @"|(?:(?:fai|fa'?)\s+)?(?:una\s+)?(?:ricerca|cerca(?:re)?)\s+"
        + @"(?:(?:busca(?:r)?|realiza(?:r)?)\s+(?:una\s+)?(?:búsqueda|busqueda)\s+)"
        + @"(?:(?:fais|effectue)\s+(?:une\s+)?(?:recherche)\s+)"
        + @"(?:(?:mach|führe)\s+(?:eine\s+)?(?:suche)\s+)"
        + @"(?:on|on the|in|su|in|en|sur|im)?\s*(?:the\s+)?(?:internet|web|online|rete)\s*"
        + @"(?:for|to|per|pour|para|um)?\s*(?:see|know|find|check|sapere|vedere|conoscere|ver|saber|trouver|finden)?\s*",
        RegexOptions.CultureInvariant)]
    private static partial Regex MultilingualSearchLeadPattern();

    [GeneratedRegex(
        @"(?i)^(?:\s*(?:please|ok|well|so|then|now|allora|ora|adesso|per\s+favore|por\s+favor|bitte)\s+)*"
        + @"(?:"
        + @"(?:do\s+a\s+)?(?:web\s+)?search(?:\s+on\s+(?:the\s+)?(?:internet|web))?"
        + @"|(?:search|lookup)\s+(?:the\s+)?(?:web|internet|online)"
        + @"|(?:on\s+)?(?:the\s+)?(?:internet|web|online|rete)\s*"
        + @"|(?:fai\s+)?(?:una\s+)?(?:ricerca|cerca(?:re)?)\s+(?:su\s+|in\s+|sul\s+)?(?:internet|web|online|rete)"
        + @"|busca(?:r)?\s+en\s+(?:internet|la\s+web)"
        + @"|recherche\s+(?:sur\s+)?(?:internet|le\s+web)"
        + @")\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex GenericSearchOnlyPattern();

    [GeneratedRegex(
        @"(?i)^("
        + @"something|anything|what|topic|now|only|just"
        + @"|qualcosa|cosa|ora|adesso|solo|argomento"
        + @"|algo|qué|ahora|solo"
        + @"|quelque\s+chose|quoi|maintenant"
        + @"|etwas|was|jetzt|nur"
        + @")$",
        RegexOptions.CultureInvariant)]
    private static partial Regex FillerOnlyPattern();

    [GeneratedRegex(
        @"(?i)(?:"
        + @"news\s+(?:about|on|regarding)|headlines\s+(?:about|on)"
        + @"|notizie\s+(?:su|relative\s+(?:a|alle?|ai))\s+|noticias\s+(?:sobre|de)\s+"
        + @"|nouvelles\s+(?:sur|concernant)\s+|nachrichten\s+(?:zu|über)\s+"
        + @")(.+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex NewsRelativePattern();

    [GeneratedRegex(
        @"(?i)\b((?:(?:latest|current|today'?s?|breaking)\s+)?(?:news|headlines|notizie|noticias|nouvelles|nachrichten)(?:\s+(?:today|oggi|hoy|now|attual(?:i|ità)?|actuales|actuelles))?(?:\s+.+)?)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex NewsClausePattern();

    [GeneratedRegex(
        @"(?i)^(?:(?:latest|current|today'?s?|breaking)\s+)?(?:news|headlines|notizie|noticias|nouvelles|nachrichten)(?:\s+(?:today|oggi|hoy|now|attual(?:i|ità)?|actuales|actuelles))?\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TopHeadlinesOnlyPattern();
}
