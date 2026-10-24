using System.Globalization;
using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.DependencyInjection;
using JarvisNet.Plugins.Sdk.Infrastructure;
using JarvisNet.Plugins.Sdk.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JarvisNet.SearchWeb;

public sealed class SearchWebPluginHostSetup : IJarvisPluginHostSetup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;

        services.AddJarvisPluginTurnHandler<SearchWebPluginTurnHandler>();

        // Configurazione client HTTP per Wikipedia
        services.AddHttpClient(
            WikipediaSummarySearchServiceRegistration.HttpClientName,
            static client => WikipediaSummarySearchServiceRegistration.ConfigureClient(client));

        // Configurazione client HTTP per DuckDuckGo
        var duckDuckGoHttpClientName = DuckDuckGoSearchServiceRegistration.HttpClientName;
        services.AddHttpClient(
            duckDuckGoHttpClientName,
            static client => DuckDuckGoSearchServiceRegistration.ConfigureClient(client));

        services.AddHttpClient(
            GoogleNewsRssSearchServiceRegistration.HttpClientName,
            static client => GoogleNewsRssSearchServiceRegistration.ConfigureClient(client));

        services.AddSingleton(sp =>
        {
            var culture = ResolveSearchCulture(sp);
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            // Configurazione Wikipedia Search Service con HttpClient gestito dal DI container
            var wikiClientName = WikipediaSummarySearchServiceRegistration.HttpClientName;
            var wikipediaSearch = new WikipediaSummarySearchService(
                factory.CreateClient(wikiClientName),
                loggerFactory.CreateLogger<WikipediaSummarySearchService>(),
                culture);

            // Configurazione DuckDuckGo Search Service con HttpClient gestito dal DI container
            var duckDuckGoClient = factory.CreateClient(duckDuckGoHttpClientName);
            var duckDuckGoSearch = new DuckDuckGoSearchService(
                duckDuckGoClient,
                loggerFactory.CreateLogger<DuckDuckGoSearchService>(),
                culture,
                wikipediaSearch: wikipediaSearch);

            var googleNewsSearch = new GoogleNewsRssSearchService(
                factory.CreateClient(GoogleNewsRssSearchServiceRegistration.HttpClientName),
                loggerFactory.CreateLogger<GoogleNewsRssSearchService>(),
                culture);

            return new InternetSearchService(
                duckDuckGoSearch,
                culture,
                googleNewsSearch);
        });
    }

    private static CultureInfo ResolveSearchCulture(IServiceProvider sp)
    {
        var options = sp.GetService<IOptions<JarvisLocaleOptions>>()?.Value;
        if (options is not null && !string.IsNullOrWhiteSpace(options.CultureName))
        {
            return JarvisLocaleResolver.CreateCulture(options.CultureName);
        }

        return CultureInfo.GetCultureInfo("en-US");
    }
}
