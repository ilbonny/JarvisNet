using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JarvisNet.SearchWeb;

public sealed class WikipediaSummarySearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WikipediaSummarySearchService> _logger;
    private readonly CultureInfo _culture;
    private readonly SearchResultLabels _labels;

    public WikipediaSummarySearchService(
        HttpClient httpClient,
        ILogger<WikipediaSummarySearchService> logger,
        CultureInfo culture)
    {
        _httpClient = httpClient;
        _logger = logger;
        _culture = culture;
        _labels = SearchResultLabelsForCulture.Get(culture);
    }

    public async Task<string?> TryFetchSummaryAsync(string query, CancellationToken cancellationToken)
    {
        var wiki = WebSearchLocaleParameters.ToWikipediaWiki(_culture);
        string? title = null;
        foreach (var attempt in SearchQueryNormalizer.GetWikipediaSearchAttempts(query))
        {
            title = await TryResolveTitleAsync(wiki, attempt, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(title))
            {
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var summaryUri =
            $"https://{wiki}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(title)}";
        using var response = await _httpClient.GetAsync(summaryUri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Wikipedia summary HTTP {StatusCode} for {Title}.", (int)response.StatusCode, title);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("extract", out var extractElement))
        {
            return null;
        }

        var extract = extractElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(extract))
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine(string.Format(_labels.WikipediaSummary, title));
        builder.AppendLine(extract);
        if (document.RootElement.TryGetProperty("content_urls", out var urls)
            && urls.TryGetProperty("desktop", out var desktop)
            && desktop.TryGetProperty("page", out var pageUrl))
        {
            var url = pageUrl.GetString();
            if (!string.IsNullOrWhiteSpace(url))
            {
                builder.AppendLine($"{_labels.Source} {url.Trim()}");
            }
        }

        _logger.LogInformation("Wikipedia summary found for query {Query} → {Title}.", query, title);
        return builder.ToString().TrimEnd();
    }

    private async Task<string?> TryResolveTitleAsync(
        string wiki,
        string query,
        CancellationToken cancellationToken)
    {
        var requestUri =
            $"https://{wiki}.wikipedia.org/w/api.php?action=opensearch&search={Uri.EscapeDataString(query)}&limit=1&namespace=0&format=json";
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() < 2)
        {
            return null;
        }

        var titles = document.RootElement[1];
        if (titles.ValueKind != JsonValueKind.Array || titles.GetArrayLength() == 0)
        {
            return null;
        }

        return titles[0].GetString()?.Trim();
    }
}

internal static class WikipediaSummarySearchServiceRegistration
{
    public const string HttpClientName = "JarvisNet.SearchWeb.Wikipedia";

    public static HttpClient ConfigureClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "JarvisNet.SearchWeb/1.0 (+https://github.com/jarvisnet)");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        return client;
    }
}
