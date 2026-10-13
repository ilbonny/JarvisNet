using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace JarvisNet.SearchWeb;

public sealed class InternetSearchService
{
    private readonly WikipediaSummarySearchService _wikipedia;
    private readonly SearchResultLabels _labels;
    private readonly ILogger<InternetSearchService> _logger;

    public InternetSearchService(
        WikipediaSummarySearchService wikipedia,
        CultureInfo culture,
        ILogger<InternetSearchService> logger)
    {
        _wikipedia = wikipedia;
        _labels = SearchResultLabelsForCulture.Get(culture);
        _logger = logger;
    }

    public async Task<string> SearchAsync(string query, int maxRelatedTopics, CancellationToken cancellationToken)
    {
        _ = maxRelatedTopics;

        var trimmed = SearchQueryNormalizer.Normalize(query.Trim());
        if (string.IsNullOrEmpty(trimmed))
        {
            return _labels.EmptyQuery;
        }

        _logger.LogInformation("Wikipedia lookup: {Query}", trimmed);

        var wiki = await _wikipedia.TryFetchSummaryAsync(trimmed, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(wiki))
        {
            return wiki;
        }

        var builder = new StringBuilder();
        builder.AppendLine(string.Format(_labels.WebSearchHeader, trimmed));
        builder.AppendLine();
        builder.AppendLine(_labels.NoWebResults);
        return builder.ToString().TrimEnd();
    }
}
