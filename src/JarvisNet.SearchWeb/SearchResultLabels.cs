using System.Globalization;

namespace JarvisNet.SearchWeb;

internal readonly record struct SearchResultLabels(
    string WebSearchHeader,
    string DirectAnswer,
    string Type,
    string Definition,
    string Source,
    string RelatedTopics,
    string NoInstantAnswer,
    string HeadlinesTop,
    string HeadlinesForQuery,
    string EmptyQuery,
    string SearchFailed,
    string UnableToParse);

internal static class SearchResultLabelsForCulture
{
    internal static SearchResultLabels Get(CultureInfo culture)
    {
        var lang = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        return lang switch
        {
            "it" => Italian,
            "es" => Spanish,
            "fr" => French,
            "de" => German,
            _ => English,
        };
    }

    private static SearchResultLabels English { get; } = new(
        WebSearchHeader: "Web search (DuckDuckGo Instant Answer) for: \"{0}\"",
        DirectAnswer: "Direct answer:",
        Type: "Type:",
        Definition: "Definition:",
        Source: "Source:",
        RelatedTopics: "Related topics:",
        NoInstantAnswer:
            "No instant answer for this query. "
            + "The DuckDuckGo API does not return full web results like a classic search engine; "
            + "try a more specific query.",
        HeadlinesTop: "Headlines (Google News):",
        HeadlinesForQuery: "Headlines for \"{0}\" (Google News):",
        EmptyQuery: "Empty search query.",
        SearchFailed: "Search failed (HTTP {0}).",
        UnableToParse: "Unable to parse DuckDuckGo response.");

    private static SearchResultLabels Italian { get; } = new(
        WebSearchHeader: "Ricerca internet (DuckDuckGo Instant Answer) per: \"{0}\"",
        DirectAnswer: "Risposta diretta:",
        Type: "Tipo:",
        Definition: "Definizione:",
        Source: "Fonte:",
        RelatedTopics: "Argomenti correlati:",
        NoInstantAnswer:
            "Nessun riassunto istantaneo per questa query. "
            + "L'API DuckDuckGo non restituisce risultati web completi come un motore classico; "
            + "prova a riformulare la domanda.",
        HeadlinesTop: "Titoli di attualità (Google News):",
        HeadlinesForQuery: "Titoli di attualità per \"{0}\" (Google News):",
        EmptyQuery: "Query di ricerca vuota.",
        SearchFailed: "Ricerca non riuscita (HTTP {0}).",
        UnableToParse: "Risposta DuckDuckGo non interpretabile.");

    private static SearchResultLabels Spanish { get; } = new(
        WebSearchHeader: "Búsqueda web (DuckDuckGo Instant Answer) para: \"{0}\"",
        DirectAnswer: "Respuesta directa:",
        Type: "Tipo:",
        Definition: "Definición:",
        Source: "Fuente:",
        RelatedTopics: "Temas relacionados:",
        NoInstantAnswer:
            "No hay respuesta instantánea para esta consulta. "
            + "La API de DuckDuckGo no devuelve resultados web completos; "
            + "prueba con una consulta más específica.",
        HeadlinesTop: "Titulares (Google News):",
        HeadlinesForQuery: "Titulares para \"{0}\" (Google News):",
        EmptyQuery: "Consulta de búsqueda vacía.",
        SearchFailed: "Búsqueda fallida (HTTP {0}).",
        UnableToParse: "No se pudo interpretar la respuesta de DuckDuckGo.");

    private static SearchResultLabels French { get; } = new(
        WebSearchHeader: "Recherche web (DuckDuckGo Instant Answer) pour : « {0} »",
        DirectAnswer: "Réponse directe :",
        Type: "Type :",
        Definition: "Définition :",
        Source: "Source :",
        RelatedTopics: "Sujets connexes :",
        NoInstantAnswer:
            "Pas de réponse instantanée pour cette requête. "
            + "L'API DuckDuckGo ne renvoie pas des résultats web complets ; "
            + "essayez une requête plus précise.",
        HeadlinesTop: "Titres (Google News) :",
        HeadlinesForQuery: "Titres pour « {0} » (Google News) :",
        EmptyQuery: "Requête de recherche vide.",
        SearchFailed: "Échec de la recherche (HTTP {0}).",
        UnableToParse: "Impossible d'interpréter la réponse DuckDuckGo.");

    private static SearchResultLabels German { get; } = new(
        WebSearchHeader: "Websuche (DuckDuckGo Instant Answer) für: \"{0}\"",
        DirectAnswer: "Direkte Antwort:",
        Type: "Typ:",
        Definition: "Definition:",
        Source: "Quelle:",
        RelatedTopics: "Verwandte Themen:",
        NoInstantAnswer:
            "Keine Sofortantwort für diese Anfrage. "
            + "Die DuckDuckGo-API liefert keine vollständigen Web-Ergebnisse; "
            + "formulieren Sie die Anfrage spezifischer.",
        HeadlinesTop: "Schlagzeilen (Google News):",
        HeadlinesForQuery: "Schlagzeilen für \"{0}\" (Google News):",
        EmptyQuery: "Leere Suchanfrage.",
        SearchFailed: "Suche fehlgeschlagen (HTTP {0}).",
        UnableToParse: "DuckDuckGo-Antwort konnte nicht gelesen werden.");
}
