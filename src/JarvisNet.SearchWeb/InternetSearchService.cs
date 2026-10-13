using System.Globalization;
using Microsoft.Extensions.Logging;

namespace JarvisNet.SearchWeb;

public sealed class InternetSearchService
{
    private readonly BraveWebSearchService _brave;
    private readonly GoogleNewsRssSearchService? _newsSearch;
    private readonly WikipediaSummarySearchService? _wikipediaSearch;
    private readonly ILogger<InternetSearchService> _logger;

    public InternetSearchService(
        BraveWebSearchService brave,
        ILogger<InternetSearchService> logger,
        GoogleNewsRssSearchService? newsSearch = null,
        WikipediaSummarySearchService? wikipediaSearch = null)
    {
        _brave = brave;
        _logger = logger;
        _newsSearch = newsSearch;
        _wikipediaSearch = wikipediaSearch;
    }

    public async Task<string> SearchAsync(string query, int maxRelatedTopics, CancellationToken cancellationToken)
    {
        var trimmed = SearchQueryNormalizer.Normalize(query.Trim());
        var outcome = await _brave.SearchAsync(trimmed, maxRelatedTopics, cancellationToken).ConfigureAwait(false);

        if (outcome.HasWebResults || outcome.IsMissingApiKey)
        {
            return outcome.Message;
        }

        return await AppendWebFallbacksAsync(trimmed, maxRelatedTopics, outcome.Message, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string> AppendWebFallbacksAsync(
        string trimmed,
        int maxRelatedTopics,
        string primary,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Brave returned no web hits for {Query}; trying Wikipedia / News fallbacks.", trimmed);

        var sections = new List<string> { primary };

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
}
