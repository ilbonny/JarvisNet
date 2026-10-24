using System.Globalization;
using System.Text;

namespace JarvisNet.SearchWeb;

/// <summary>
/// Ricerca web: DuckDuckGo (+ Wikipedia in fallback) oppure Google News RSS solo per intent notizie.
/// </summary>
public sealed class InternetSearchService
{
    private readonly DuckDuckGoSearchService _duckDuckGo;
    private readonly GoogleNewsRssSearchService? _newsSearch;
    private readonly CultureInfo _culture;
    private readonly SearchResultLabels _labels;

    public InternetSearchService(
        DuckDuckGoSearchService duckDuckGo,
        CultureInfo culture,
        GoogleNewsRssSearchService? newsSearch = null)
    {
        _duckDuckGo = duckDuckGo ?? throw new ArgumentNullException(nameof(duckDuckGo));
        _culture = culture ?? CultureInfo.InvariantCulture;
        _newsSearch = newsSearch;
        _labels = SearchResultLabelsForCulture.Get(_culture);
    }

    public async Task<string> SearchAsync(string query, int maxRelatedTopics, CancellationToken cancellationToken)
    {
        var trimmed = SearchQueryNormalizer.Normalize(query.Trim());

        if (string.IsNullOrEmpty(trimmed))
        {
            return _labels.EmptyQuery;
        }

        maxRelatedTopics = Math.Clamp(maxRelatedTopics, 1, 15);

        if (SearchQueryNormalizer.IsNewsQuery(trimmed)
            || SearchQueryNormalizer.IsNewsQuery(query))
        {
            return await SearchNewsAsync(trimmed, maxRelatedTopics, cancellationToken).ConfigureAwait(false);
        }

        return await _duckDuckGo.SearchAsync(trimmed, maxRelatedTopics, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SearchNewsAsync(
        string trimmed,
        int maxRelatedTopics,
        CancellationToken cancellationToken)
    {
        if (_newsSearch is null)
        {
            var unavailable = new StringBuilder();
            unavailable.AppendLine(string.Format(_labels.WebSearchHeader, trimmed));
            unavailable.AppendLine();
            unavailable.AppendLine(_labels.NoWebResults);
            return unavailable.ToString().TrimEnd();
        }

        var headlines = await _newsSearch
            .TryFetchHeadlinesAsync(trimmed, maxRelatedTopics, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(headlines))
        {
            return headlines;
        }

        var empty = new StringBuilder();
        empty.AppendLine(string.Format(_labels.WebSearchHeader, trimmed));
        empty.AppendLine();
        empty.AppendLine(_labels.NoWebResults);
        return empty.ToString().TrimEnd();
    }
}
