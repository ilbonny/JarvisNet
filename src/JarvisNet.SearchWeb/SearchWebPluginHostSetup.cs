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
        services.AddOptions<SearchWebOptions>()
            .Bind(configuration.GetSection(SearchWebOptions.SectionName));

        services.AddJarvisPluginTurnHandler<SearchWebPluginTurnHandler>();
        services.AddHttpClient(
            BraveWebSearchServiceRegistration.HttpClientName,
            static client => BraveWebSearchServiceRegistration.ConfigureClient(client));
        services.AddHttpClient(
            GoogleNewsRssSearchServiceRegistration.HttpClientName,
            static client => GoogleNewsRssSearchServiceRegistration.ConfigureClient(client));
        services.AddHttpClient(
            WikipediaSummarySearchServiceRegistration.HttpClientName,
            static client => WikipediaSummarySearchServiceRegistration.ConfigureClient(client));

        services.AddSingleton(sp =>
        {
            var culture = ResolveSearchCulture(sp);
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var options = sp.GetRequiredService<IOptionsMonitor<SearchWebOptions>>();

            var newsClient = GoogleNewsRssSearchServiceRegistration.ConfigureClient(
                factory.CreateClient(GoogleNewsRssSearchServiceRegistration.HttpClientName));
            var newsSearch = new GoogleNewsRssSearchService(
                newsClient,
                loggerFactory.CreateLogger<GoogleNewsRssSearchService>(),
                culture);
            var wikiClient = WikipediaSummarySearchServiceRegistration.ConfigureClient(
                factory.CreateClient(WikipediaSummarySearchServiceRegistration.HttpClientName));
            var wikipediaSearch = new WikipediaSummarySearchService(
                wikiClient,
                loggerFactory.CreateLogger<WikipediaSummarySearchService>(),
                culture);
            var braveClient = BraveWebSearchServiceRegistration.ConfigureClient(
                factory.CreateClient(BraveWebSearchServiceRegistration.HttpClientName));
            var braveSearch = new BraveWebSearchService(
                braveClient,
                loggerFactory.CreateLogger<BraveWebSearchService>(),
                options,
                culture);

            return new InternetSearchService(
                braveSearch,
                loggerFactory.CreateLogger<InternetSearchService>(),
                newsSearch,
                wikipediaSearch);
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
