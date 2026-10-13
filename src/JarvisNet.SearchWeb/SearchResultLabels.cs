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
    string UnableToParse,
    string MissingApiKey);

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
        WebSearchHeader: "Web search (Brave Search) for: \"{0}\"",
        DirectAnswer: "Direct answer:",
        Type: "Type:",
        Definition: "Definition:",
        Source: "Source:",
        RelatedTopics: "Related topics:",
        WebLinks: "Web results:",
        WikipediaSummary: "Wikipedia — {0}:",
        NoWebResults: "No web results for this query.",
        HeadlinesTop: "Headlines (Google News):",
        HeadlinesForQuery: "Headlines for \"{0}\" (Google News):",
        EmptyQuery: "Empty search query.",
        SearchFailed: "Search failed (HTTP {0}).",
        UnableToParse: "Unable to parse Brave Search response.",
        MissingApiKey:
            "Brave Search API key missing. Set JarvisNet:SearchWeb:ApiKey in config/jarvisnet.searchweb.local.json "
            + "or the BRAVE_SEARCH_API_KEY environment variable. See README for how to obtain a key.");

    private static SearchResultLabels Italian { get; } = new(
        WebSearchHeader: "Ricerca internet (Brave Search) per: \"{0}\"",
        DirectAnswer: "Risposta diretta:",
        Type: "Tipo:",
        Definition: "Definizione:",
        Source: "Fonte:",
        RelatedTopics: "Argomenti correlati:",
        WebLinks: "Risultati web:",
        WikipediaSummary: "Wikipedia — {0}:",
        NoWebResults: "Nessun risultato web per questa query.",
        HeadlinesTop: "Titoli di attualità (Google News):",
        HeadlinesForQuery: "Titoli di attualità per \"{0}\" (Google News):",
        EmptyQuery: "Query di ricerca vuota.",
        SearchFailed: "Ricerca non riuscita (HTTP {0}).",
        UnableToParse: "Risposta Brave Search non interpretabile.",
        MissingApiKey:
            "Chiave API Brave Search mancante. Imposta JarvisNet:SearchWeb:ApiKey in config/jarvisnet.searchweb.local.json "
            + "oppure la variabile d'ambiente BRAVE_SEARCH_API_KEY. Vedi README per ottenere la chiave.");

    private static SearchResultLabels Spanish { get; } = new(
        WebSearchHeader: "Búsqueda web (Brave Search) para: \"{0}\"",
        DirectAnswer: "Respuesta directa:",
        Type: "Tipo:",
        Definition: "Definición:",
        Source: "Fuente:",
        RelatedTopics: "Temas relacionados:",
        WebLinks: "Resultados web:",
        WikipediaSummary: "Wikipedia — {0}:",
        NoWebResults: "No hay resultados web para esta consulta.",
        HeadlinesTop: "Titulares (Google News):",
        HeadlinesForQuery: "Titulares para \"{0}\" (Google News):",
        EmptyQuery: "Consulta de búsqueda vacía.",
        SearchFailed: "Búsqueda fallida (HTTP {0}).",
        UnableToParse: "No se pudo interpretar la respuesta de Brave Search.",
        MissingApiKey:
            "Falta la clave API de Brave Search. Configura JarvisNet:SearchWeb:ApiKey en config/jarvisnet.searchweb.local.json "
            + "o la variable de entorno BRAVE_SEARCH_API_KEY. Consulta el README.");

    private static SearchResultLabels French { get; } = new(
        WebSearchHeader: "Recherche web (Brave Search) pour : « {0} »",
        DirectAnswer: "Réponse directe :",
        Type: "Type :",
        Definition: "Définition :",
        Source: "Source :",
        RelatedTopics: "Sujets connexes :",
        WebLinks: "Résultats web :",
        WikipediaSummary: "Wikipédia — {0} :",
        NoWebResults: "Aucun résultat web pour cette requête.",
        HeadlinesTop: "Titres (Google News) :",
        HeadlinesForQuery: "Titres pour « {0} » (Google News) :",
        EmptyQuery: "Requête de recherche vide.",
        SearchFailed: "Échec de la recherche (HTTP {0}).",
        UnableToParse: "Impossible d'interpréter la réponse Brave Search.",
        MissingApiKey:
            "Clé API Brave Search manquante. Définissez JarvisNet:SearchWeb:ApiKey dans config/jarvisnet.searchweb.local.json "
            + "ou la variable d'environnement BRAVE_SEARCH_API_KEY. Voir le README.");

    private static SearchResultLabels German { get; } = new(
        WebSearchHeader: "Websuche (Brave Search) für: \"{0}\"",
        DirectAnswer: "Direkte Antwort:",
        Type: "Typ:",
        Definition: "Definition:",
        Source: "Quelle:",
        RelatedTopics: "Verwandte Themen:",
        WebLinks: "Web-Ergebnisse:",
        WikipediaSummary: "Wikipedia — {0}:",
        NoWebResults: "Keine Web-Ergebnisse für diese Anfrage.",
        HeadlinesTop: "Schlagzeilen (Google News):",
        HeadlinesForQuery: "Schlagzeilen für \"{0}\" (Google News):",
        EmptyQuery: "Leere Suchanfrage.",
        SearchFailed: "Suche fehlgeschlagen (HTTP {0}).",
        UnableToParse: "Brave-Search-Antwort konnte nicht gelesen werden.",
        MissingApiKey:
            "Brave-Search-API-Schlüssel fehlt. Setzen Sie JarvisNet:SearchWeb:ApiKey in config/jarvisnet.searchweb.local.json "
            + "oder die Umgebungsvariable BRAVE_SEARCH_API_KEY. Siehe README.");
}
