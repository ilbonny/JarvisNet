using System.ComponentModel;
using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace JarvisNet.SearchWeb;

[JarvisPlugin(
    "SearchWeb",
    "1.0.0",
    "Web search via DuckDuckGo Instant Answer and optional news RSS fallback.")]
public sealed class SearchWebPlugin : IJarvisPlugin
{
    private readonly DuckDuckGoSearchService _search;
    private readonly ILogger<SearchWebPlugin> _logger;

    public SearchWebPlugin(DuckDuckGoSearchService search, ILogger<SearchWebPlugin> logger)
    {
        _search = search;
        _logger = logger;
    }

    [KernelFunction("search_internet")]
    [Description(
        "REQUIRED when the user wants a web search, online lookup, news headlines, or factual web information. "
        + "Call immediately with a concrete query (e.g. today's news, HDMI cables). "
        + "Do NOT use for opening sites or clicking in the MCP browser.")]
    public Task<string> SearchInternetAsync(
        [Description("Natural-language search query")] string query,
        [Description("Max related topics to include (1-15)")] int maxRelatedTopics = 8,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SearchWeb.search_internet invoked (query: {Query}).", query);
        return _search.SearchAsync(query, maxRelatedTopics, cancellationToken);
    }
}
