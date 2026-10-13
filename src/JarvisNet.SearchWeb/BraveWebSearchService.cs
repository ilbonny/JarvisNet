using System.Globalization;
using System.Text;
using System.Text.Json;
using JarvisNet.SearchWeb.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JarvisNet.SearchWeb;

public sealed class BraveWebSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<BraveWebSearchService> _logger;
    private readonly IOptionsMonitor<SearchWebOptions> _options;
    private readonly CultureInfo _culture;
    private readonly SearchResultLabels _labels;

    public BraveWebSearchService(
        HttpClient httpClient,
        ILogger<BraveWebSearchService> logger,
        IOptionsMonitor<SearchWebOptions> options,
        CultureInfo culture)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options;
        _culture = culture;
        _labels = SearchResultLabelsForCulture.Get(culture);
    }

    public async Task<BraveSearchOutcome> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var trimmed = SearchQueryNormalizer.Normalize(query.Trim());
        if (string.IsNullOrEmpty(trimmed))
        {
            return BraveSearchOutcome.FromMessage(_labels.EmptyQuery);
        }

        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Brave Search API key is not configured.");
            return BraveSearchOutcome.MissingApiKey(_labels.MissingApiKey);
        }

        maxResults = Math.Clamp(maxResults, 1, 20);
        var (country, searchLang) = WebSearchLocaleParameters.ToBrave(_culture, _options.CurrentValue);

        var requestUri =
            $"?q={Uri.EscapeDataString(trimmed)}"
            + $"&count={maxResults}"
            + $"&country={Uri.EscapeDataString(country)}"
            + $"&search_lang={Uri.EscapeDataString(searchLang)}";

        _logger.LogInformation(
            "Brave Search: {Query} (culture {Culture}, country {Country}, lang {Lang})",
            trimmed,
            _culture.Name,
            country,
            searchLang);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("X-Subscription-Token", apiKey);

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Brave Search HTTP {StatusCode} for query {Query}.",
                (int)response.StatusCode,
                trimmed);
            return BraveSearchOutcome.FromMessage(string.Format(_labels.SearchFailed, (int)response.StatusCode));
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("Brave Search returned an empty body for query {Query}.", trimmed);
            return BuildNoResultsOutcome(trimmed);
        }

        BraveWebSearchResponse? payload;
        try
        {
            payload = JsonSerializer.Deserialize<BraveWebSearchResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Brave Search returned non-JSON content for query {Query}.", trimmed);
            return BraveSearchOutcome.FromMessage(_labels.UnableToParse);
        }

        var items = payload?.Web?.Results ?? [];
        if (items.Count == 0)
        {
            return BuildNoResultsOutcome(trimmed);
        }

        var formatted = FormatResults(trimmed, items, maxResults, _labels);
        return BraveSearchOutcome.FromMessage(formatted, hasWebResults: true);
    }

    private string? ResolveApiKey()
    {
        var fromOptions = _options.CurrentValue.ApiKey;
        if (!string.IsNullOrWhiteSpace(fromOptions))
        {
            return fromOptions.Trim();
        }

        return Environment.GetEnvironmentVariable("BRAVE_SEARCH_API_KEY")?.Trim();
    }

    internal static string FormatResults(
        string query,
        IReadOnlyList<BraveWebResultItem> items,
        int maxResults,
        SearchResultLabels labels)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Format(labels.WebSearchHeader, query));
        builder.AppendLine();
        builder.AppendLine(labels.WebLinks);

        var count = 0;
        foreach (var item in items)
        {
            if (count >= maxResults)
            {
                break;
            }

            var title = item.Title?.Trim();
            var url = item.Url?.Trim();
            var description = item.Description?.Trim();

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(url))
            {
                continue;
            }

            count++;
            var headline = string.IsNullOrEmpty(title) ? url : title;
            builder.Append("- ").Append(headline);
            if (!string.IsNullOrEmpty(url) && headline != url)
            {
                builder.Append(" (").Append(url).Append(')');
            }

            builder.AppendLine();

            if (!string.IsNullOrEmpty(description))
            {
                builder.AppendLine("  " + description);
            }
        }

        if (count == 0)
        {
            builder.AppendLine(labels.NoWebResults);
        }

        return builder.ToString().TrimEnd();
    }

    private BraveSearchOutcome BuildNoResultsOutcome(string query)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Format(_labels.WebSearchHeader, query));
        builder.AppendLine();
        builder.AppendLine(_labels.NoWebResults);
        return BraveSearchOutcome.FromMessage(builder.ToString().TrimEnd());
    }
}

public readonly record struct BraveSearchOutcome(string Message, bool HasWebResults, bool IsMissingApiKey)
{
    public static BraveSearchOutcome FromMessage(string message, bool hasWebResults = false) =>
        new(message, hasWebResults, IsMissingApiKey: false);

    public static BraveSearchOutcome MissingApiKey(string message) =>
        new(message, HasWebResults: false, IsMissingApiKey: true);
}

internal static class BraveWebSearchServiceRegistration
{
    public const string HttpClientName = "JarvisNet.SearchWeb.Brave";

    public static HttpClient ConfigureClient(HttpClient client)
    {
        client.BaseAddress = new Uri("https://api.search.brave.com/res/v1/web/search");
        client.Timeout = TimeSpan.FromSeconds(25);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "JarvisNet.SearchWeb/1.0 (+https://github.com/jarvisnet)");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        return client;
    }
}
