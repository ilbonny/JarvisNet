using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace JarvisNet.SearchWeb;

public sealed class GoogleNewsRssSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleNewsRssSearchService> _logger;
    private readonly CultureInfo _culture;
    private readonly SearchResultLabels _labels;

    public GoogleNewsRssSearchService(
        HttpClient httpClient,
        ILogger<GoogleNewsRssSearchService> logger,
        CultureInfo culture)
    {
        _httpClient = httpClient;
        _logger = logger;
        _culture = culture;
        _labels = SearchResultLabelsForCulture.Get(culture);
    }

    public async Task<string?> TryFetchHeadlinesAsync(
        string query,
        int maxItems,
        CancellationToken cancellationToken)
    {
        maxItems = Math.Clamp(maxItems, 1, 12);
        var useTopFeed = SearchQueryNormalizer.PreferTopHeadlinesFeed(query);
        var locale = WebSearchLocaleParameters.ToGoogleNews(_culture).QueryString;
        var requestUri = useTopFeed
            ? $"rss?{locale}"
            : $"rss/search?q={Uri.EscapeDataString(query)}&{locale}";

        _logger.LogInformation(
            "Google News RSS ({FeedType}) for: {Query} (culture {Culture})",
            useTopFeed ? "top" : "search",
            query,
            _culture.Name);

        using var response = await _httpClient
            .GetAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google News RSS HTTP {StatusCode}.", (int)response.StatusCode);
            return null;
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await ReadRssItemsAsync(stream, maxItems, cancellationToken).ConfigureAwait(false);
        if (items.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine(useTopFeed
            ? _labels.HeadlinesTop
            : string.Format(_labels.HeadlinesForQuery, query));
        foreach (var item in items)
        {
            builder.Append("- ").AppendLine(item);
        }

        return builder.ToString().TrimEnd();
    }

    private static async Task<List<string>> ReadRssItemsAsync(
        Stream stream,
        int maxItems,
        CancellationToken cancellationToken)
    {
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken)
            .ConfigureAwait(false);

        return document.Descendants("item")
            .Select(item => item.Element("title")?.Value)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Select(title => CleanTitle(title!))
            .Take(maxItems)
            .ToList();
    }

    private static string CleanTitle(string title) =>
        title.Replace("\n", " ", StringComparison.Ordinal).Trim();
}

internal static class GoogleNewsRssSearchServiceRegistration
{
    public const string HttpClientName = "JarvisNet.SearchWeb.GoogleNews";

    public static HttpClient ConfigureClient(HttpClient client)
    {
        client.BaseAddress = new Uri("https://news.google.com/");
        client.Timeout = TimeSpan.FromSeconds(25);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "JarvisNet.SearchWeb/1.0 (+https://github.com/jarvisnet)");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/rss+xml, application/xml, text/xml");
        return client;
    }
}
