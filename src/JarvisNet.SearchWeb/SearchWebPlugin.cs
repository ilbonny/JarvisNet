using System.ComponentModel;
using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace JarvisNet.SearchWeb;

[JarvisPlugin(
    "SearchWeb",
    "1.0.0",
    "Ricerca su internet tramite API DuckDuckGo (Instant Answer).")]
public sealed class SearchWebPlugin : IJarvisPlugin
{
    private readonly DuckDuckGoSearchService _search;
    private readonly ILogger<SearchWebPlugin> _logger;

    public SearchWebPlugin(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        ILogger<SearchWebPlugin> logger)
    {
        var httpClient = DuckDuckGoSearchServiceRegistration.ConfigureClient(
            httpClientFactory.CreateClient(DuckDuckGoSearchServiceRegistration.HttpClientName));
        var newsClient = GoogleNewsRssSearchServiceRegistration.ConfigureClient(
            httpClientFactory.CreateClient(GoogleNewsRssSearchServiceRegistration.HttpClientName));
        var newsSearch = new GoogleNewsRssSearchService(
            newsClient,
            loggerFactory.CreateLogger<GoogleNewsRssSearchService>());

        _search = new DuckDuckGoSearchService(
            httpClient,
            loggerFactory.CreateLogger<DuckDuckGoSearchService>(),
            newsSearch);
        _logger = logger;
    }

    [KernelFunction("search_internet")]
    [Description(
        "OBBLIGATORIO per richieste tipo «cerca su internet», «cerca online», «ricerca web», notizie o informazioni sul web. "
        + "Esegui subito con una query concreta (es. «notizie di oggi», «cavi HDMI»). "
        + "Non usare per aprire siti o cliccare nel browser MCP.")]
    public Task<string> SearchInternetAsync(
        [Description("Testo da cercare, in linguaggio naturale")] string query,
        [Description("Numero massimo di argomenti correlati da includere (1-15)")] int maxRelatedTopics = 8,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SearchWeb.search_internet eseguito (query: {Query}).", query);
        return _search.SearchAsync(query, maxRelatedTopics, cancellationToken);
    }
}
