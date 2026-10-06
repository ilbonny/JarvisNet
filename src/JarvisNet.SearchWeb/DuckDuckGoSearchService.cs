using System.Text;
using System.Text.Json;
using JarvisNet.SearchWeb.Models;
using Microsoft.Extensions.Logging;

namespace JarvisNet.SearchWeb;

internal sealed class DuckDuckGoSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<DuckDuckGoSearchService> _logger;
    private readonly GoogleNewsRssSearchService? _newsSearch;

    public DuckDuckGoSearchService(
        HttpClient httpClient,
        ILogger<DuckDuckGoSearchService> logger,
        GoogleNewsRssSearchService? newsSearch = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _newsSearch = newsSearch;
    }

    public async Task<string> SearchAsync(string query, int maxRelatedTopics, CancellationToken cancellationToken)
    {
        var trimmed = SearchQueryNormalizer.Normalize(query.Trim());
        if (string.IsNullOrEmpty(trimmed))
        {
            return "Query di ricerca vuota.";
        }

        maxRelatedTopics = Math.Clamp(maxRelatedTopics, 1, 15);

        var requestUri =
            $"?q={Uri.EscapeDataString(trimmed)}&format=json&no_html=1&skip_disambig=1&kl=it-it";

        _logger.LogInformation("Ricerca DuckDuckGo: {Query}", trimmed);

        using var response = await _httpClient
            .GetAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "DuckDuckGo HTTP {StatusCode} per query {Query}.",
                (int)response.StatusCode,
                trimmed);
            return $"Ricerca non riuscita (HTTP {(int)response.StatusCode}).";
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var payload = await JsonSerializer
            .DeserializeAsync<DuckDuckGoInstantAnswer>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (payload is null)
        {
            return "Risposta DuckDuckGo non interpretabile.";
        }

        var instant = FormatAnswer(trimmed, payload, maxRelatedTopics);
        if (HasSubstantiveInstantAnswer(payload) || _newsSearch is null)
        {
            return instant;
        }

        if (!SearchQueryNormalizer.IsNewsQuery(trimmed))
        {
            return instant;
        }

        var headlines = await _newsSearch
            .TryFetchHeadlinesAsync(trimmed, maxRelatedTopics, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(headlines))
        {
            return instant;
        }

        return instant + Environment.NewLine + Environment.NewLine + headlines;
    }

    internal static bool HasSubstantiveInstantAnswer(DuckDuckGoInstantAnswer payload) =>
        !string.IsNullOrWhiteSpace(payload.Answer)
        || !string.IsNullOrWhiteSpace(payload.Definition)
        || !string.IsNullOrWhiteSpace(payload.AbstractText)
        || payload.RelatedTopics.Count > 0;

    internal static string FormatAnswer(string query, DuckDuckGoInstantAnswer payload, int maxRelatedTopics)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Ricerca internet (DuckDuckGo Instant Answer) per: \"{query}\"");

        if (!string.IsNullOrWhiteSpace(payload.Answer))
        {
            builder.AppendLine();
            builder.Append("Risposta diretta: ").AppendLine(payload.Answer.Trim());
            if (!string.IsNullOrWhiteSpace(payload.AnswerType))
            {
                builder.AppendLine($"Tipo: {payload.AnswerType.Trim()}");
            }
        }

        if (!string.IsNullOrWhiteSpace(payload.Definition))
        {
            builder.AppendLine();
            builder.Append("Definizione: ").AppendLine(payload.Definition.Trim());
            if (!string.IsNullOrWhiteSpace(payload.DefinitionURL))
            {
                builder.AppendLine($"Fonte: {payload.DefinitionURL.Trim()}");
            }
        }

        if (!string.IsNullOrWhiteSpace(payload.AbstractText))
        {
            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(payload.Heading))
            {
                builder.AppendLine(payload.Heading.Trim());
            }

            builder.AppendLine(payload.AbstractText.Trim());
            if (!string.IsNullOrWhiteSpace(payload.AbstractSource))
            {
                builder.Append("Fonte: ").Append(payload.AbstractSource.Trim());
            }

            if (!string.IsNullOrWhiteSpace(payload.AbstractURL))
            {
                builder.Append(" — ").AppendLine(payload.AbstractURL.Trim());
            }
            else
            {
                builder.AppendLine();
            }
        }

        var relatedLines = CollectRelatedTopics(payload.RelatedTopics, maxRelatedTopics);
        if (relatedLines.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Argomenti correlati:");
            foreach (var line in relatedLines)
            {
                builder.AppendLine("- " + line);
            }
        }

        if (builder.Length <= query.Length + 80
            && string.IsNullOrWhiteSpace(payload.Answer)
            && string.IsNullOrWhiteSpace(payload.AbstractText)
            && relatedLines.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine(
                "Nessun riassunto istantaneo per questa query. "
                + "L'API DuckDuckGo non restituisce risultati web completi come un motore di ricerca classico; "
                + "prova a riformulare la domanda in modo più specifico.");
        }

        return builder.ToString().TrimEnd();
    }

    private static List<string> CollectRelatedTopics(
        IEnumerable<DuckDuckGoRelatedTopic> topics,
        int maxItems)
    {
        var lines = new List<string>(maxItems);
        WalkTopics(topics, lines, maxItems);
        return lines;
    }

    private static void WalkTopics(
        IEnumerable<DuckDuckGoRelatedTopic> topics,
        List<string> lines,
        int maxItems)
    {
        foreach (var topic in topics)
        {
            if (lines.Count >= maxItems)
            {
                return;
            }

            if (topic.Topics is { Count: > 0 })
            {
                WalkTopics(topic.Topics, lines, maxItems);
                continue;
            }

            var text = topic.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var url = topic.FirstURL?.Trim();
            lines.Add(string.IsNullOrEmpty(url) ? text : $"{text} ({url})");
        }
    }
}

internal static class DuckDuckGoSearchServiceRegistration
{
    public const string HttpClientName = "JarvisNet.SearchWeb.DuckDuckGo";

    public static HttpClient ConfigureClient(HttpClient client)
    {
        client.BaseAddress = new Uri("https://api.duckduckgo.com/");
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "JarvisNet.SearchWeb/1.0 (+https://github.com/jarvisnet)");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        return client;
    }
}
