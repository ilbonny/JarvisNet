using System.Globalization;
using System.Text;
using System.Text.Json;
using JarvisNet.SearchWeb.Models;
using Microsoft.Extensions.Logging;

namespace JarvisNet.SearchWeb;

public sealed class DuckDuckGoSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<DuckDuckGoSearchService> _logger;
    private readonly GoogleNewsRssSearchService? _newsSearch;
    private readonly WikipediaSummarySearchService? _wikipediaSearch;
    private readonly CultureInfo _culture;
    private readonly SearchResultLabels _labels;

    public DuckDuckGoSearchService(
        HttpClient httpClient,
        ILogger<DuckDuckGoSearchService> logger,
        CultureInfo culture,
        GoogleNewsRssSearchService? newsSearch = null,
        WikipediaSummarySearchService? wikipediaSearch = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _culture = culture;
        _labels = SearchResultLabelsForCulture.Get(culture);
        _newsSearch = newsSearch;
        _wikipediaSearch = wikipediaSearch;
    }

    public async Task<string> SearchAsync(string query, int maxRelatedTopics, CancellationToken cancellationToken)
    {
        var trimmed = SearchQueryNormalizer.Normalize(query.Trim());
        if (string.IsNullOrEmpty(trimmed))
        {
            return _labels.EmptyQuery;
        }

        maxRelatedTopics = Math.Clamp(maxRelatedTopics, 1, 15);

        var kl = WebSearchLocaleParameters.ToDuckDuckGoKl(_culture);
        var requestUri =
            $"?q={Uri.EscapeDataString(trimmed)}&format=json&no_html=1&skip_disambig=1"
            + (string.IsNullOrEmpty(kl) ? string.Empty : $"&kl={Uri.EscapeDataString(kl)}");

        _logger.LogInformation("DuckDuckGo search: {Query} (culture {Culture})", trimmed, _culture.Name);

        using var response = await _httpClient
            .GetAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "DuckDuckGo HTTP {StatusCode} for query {Query}.",
                (int)response.StatusCode,
                trimmed);
            return string.Format(_labels.SearchFailed, (int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("DuckDuckGo returned an empty body for query {Query}.", trimmed);
            return await AppendWebFallbacksAsync(
                    trimmed,
                    maxRelatedTopics,
                    _labels.UnableToParse,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        DuckDuckGoInstantAnswer? payload;
        try
        {
            payload = JsonSerializer.Deserialize<DuckDuckGoInstantAnswer>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "DuckDuckGo returned non-JSON content for query {Query} (length {Length}).",
                trimmed,
                body.Length);
            return await AppendWebFallbacksAsync(
                    trimmed,
                    maxRelatedTopics,
                    _labels.UnableToParse,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (payload is null)
        {
            return await AppendWebFallbacksAsync(
                    trimmed,
                    maxRelatedTopics,
                    _labels.UnableToParse,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var instant = FormatAnswer(trimmed, payload, maxRelatedTopics, _labels);
        if (HasSubstantiveInstantAnswer(payload))
        {
            return instant;
        }

        return await AppendWebFallbacksAsync(trimmed, maxRelatedTopics, instant, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string> AppendWebFallbacksAsync(
        string trimmed,
        int maxRelatedTopics,
        string instant,
        CancellationToken cancellationToken)
    {
        var sections = new List<string> { instant };

        if (_wikipediaSearch is not null)
        {
            var wiki = await _wikipediaSearch
                .TryFetchSummaryAsync(trimmed, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(wiki))
            {
                sections.Add(wiki);
            }
        }

        if (_newsSearch is not null)
        {
            var headlines = await _newsSearch
                .TryFetchHeadlinesAsync(trimmed, maxRelatedTopics, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(headlines))
            {
                sections.Add(headlines);
            }
        }

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    internal static bool HasSubstantiveInstantAnswer(DuckDuckGoInstantAnswer payload) =>
        !string.IsNullOrWhiteSpace(payload.Answer)
        || !string.IsNullOrWhiteSpace(payload.Definition)
        || !string.IsNullOrWhiteSpace(payload.AbstractText)
        || payload.RelatedTopics.Count > 0
        || payload.Results.Count > 0;

    internal static string FormatAnswer(
        string query,
        DuckDuckGoInstantAnswer payload,
        int maxRelatedTopics,
        SearchResultLabels labels)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Format(labels.WebSearchHeader, query));

        if (!string.IsNullOrWhiteSpace(payload.Answer))
        {
            builder.AppendLine();
            builder.Append(labels.DirectAnswer).Append(' ').AppendLine(payload.Answer.Trim());
            if (!string.IsNullOrWhiteSpace(payload.AnswerType))
            {
                builder.AppendLine($"{labels.Type} {payload.AnswerType.Trim()}");
            }
        }

        if (!string.IsNullOrWhiteSpace(payload.Definition))
        {
            builder.AppendLine();
            builder.Append(labels.Definition).Append(' ').AppendLine(payload.Definition.Trim());
            if (!string.IsNullOrWhiteSpace(payload.DefinitionURL))
            {
                builder.AppendLine($"{labels.Source} {payload.DefinitionURL.Trim()}");
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
                builder.Append(labels.Source).Append(' ').Append(payload.AbstractSource.Trim());
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
            builder.AppendLine(labels.RelatedTopics);
            foreach (var line in relatedLines)
            {
                builder.AppendLine("- " + line);
            }
        }

        var resultLines = CollectRelatedTopics(payload.Results, maxRelatedTopics);
        if (resultLines.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(labels.WebLinks);
            foreach (var line in resultLines)
            {
                builder.AppendLine("- " + line);
            }
        }

        if (builder.Length <= query.Length + 80
            && string.IsNullOrWhiteSpace(payload.Answer)
            && string.IsNullOrWhiteSpace(payload.AbstractText)
            && relatedLines.Count == 0
            && resultLines.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine(labels.NoWebResults);
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
