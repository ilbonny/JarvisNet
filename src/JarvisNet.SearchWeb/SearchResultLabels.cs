using System.Globalization;

namespace JarvisNet.SearchWeb;

internal readonly record struct SearchResultLabels(
    string WebSearchHeader,
    string DirectAnswer,
    string Type,
    string Definition,
    string Source,
    string RelatedTopics,
    string WebLinks,
    string WikipediaSummary,
    string NoWebResults,
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
        WebSearchHeader: "Wikipedia lookup for: \"{0}\"",
        DirectAnswer: "Direct answer:",
        Type: "Type:",
        Definition: "Definition:",
        Source: "Source:",
        RelatedTopics: "Related topics:",
        WebLinks: "Web results:",
        WikipediaSummary: "Wikipedia — {0}:",
        NoWebResults: "No matching Wikipedia article was found.",
        HeadlinesTop: "Headlines (Google News):",
        HeadlinesForQuery: "Headlines for \"{0}\" (Google News):",
        EmptyQuery: "Empty search query.",
        SearchFailed: "Search failed (HTTP {0}).",
        UnableToParse: "Unable to parse search response.");

    private static SearchResultLabels Italian { get; } = new(
        WebSearchHeader: "Ricerca Wikipedia per: \"{0}\"",
        DirectAnswer: "Risposta diretta:",
        Type: "Tipo:",
        Definition: "Definizione:",
        Source: "Fonte:",
        RelatedTopics: "Argomenti correlati:",
        WebLinks: "Risultati web:",
        WikipediaSummary: "Wikipedia — {0}:",
        NoWebResults: "Nessuna voce Wikipedia corrispondente trovata.",
        HeadlinesTop: "Titoli di attualità (Google News):",
        HeadlinesForQuery: "Titoli di attualità per \"{0}\" (Google News):",
        EmptyQuery: "Query di ricerca vuota.",
        SearchFailed: "Ricerca non riuscita (HTTP {0}).",
        UnableToParse: "Risposta non interpretabile.");

    private static SearchResultLabels Spanish { get; } = new(
        WebSearchHeader: "Búsqueda en Wikipedia para: \"{0}\"",
        DirectAnswer: "Respuesta directa:",
        Type: "Tipo:",
        Definition: "Definición:",
        Source: "Fuente:",
        RelatedTopics: "Temas relacionados:",
        WebLinks: "Resultados web:",
        WikipediaSummary: "Wikipedia — {0}:",
        NoWebResults: "No se encontró ningún artículo de Wikipedia.",
        HeadlinesTop: "Titulares (Google News):",
        HeadlinesForQuery: "Titulares para \"{0}\" (Google News):",
        EmptyQuery: "Consulta de búsqueda vacía.",
        SearchFailed: "Búsqueda fallida (HTTP {0}).",
        UnableToParse: "No se pudo interpretar la respuesta.");

    private static SearchResultLabels French { get; } = new(
        WebSearchHeader: "Recherche Wikipédia pour : « {0} »",
        DirectAnswer: "Réponse directe :",
        Type: "Type :",
        Definition: "Définition :",
        Source: "Source :",
        RelatedTopics: "Sujets connexes :",
        WebLinks: "Résultats web :",
        WikipediaSummary: "Wikipédia — {0} :",
        NoWebResults: "Aucun article Wikipédia correspondant.",
        HeadlinesTop: "Titres (Google News) :",
        HeadlinesForQuery: "Titres pour « {0} » (Google News) :",
        EmptyQuery: "Requête de recherche vide.",
        SearchFailed: "Échec de la recherche (HTTP {0}).",
        UnableToParse: "Impossible d'interpréter la réponse.");

    private static SearchResultLabels German { get; } = new(
        WebSearchHeader: "Wikipedia-Suche für: \"{0}\"",
        DirectAnswer: "Direkte Antwort:",
        Type: "Typ:",
        Definition: "Definition:",
        Source: "Quelle:",
        RelatedTopics: "Verwandte Themen:",
        WebLinks: "Web-Ergebnisse:",
        WikipediaSummary: "Wikipedia — {0}:",
        NoWebResults: "Kein passender Wikipedia-Artikel gefunden.",
        HeadlinesTop: "Schlagzeilen (Google News):",
        HeadlinesForQuery: "Schlagzeilen für \"{0}\" (Google News):",
        EmptyQuery: "Leere Suchanfrage.",
        SearchFailed: "Suche fehlgeschlagen (HTTP {0}).",
        UnableToParse: "Antwort konnte nicht gelesen werden.");
}
