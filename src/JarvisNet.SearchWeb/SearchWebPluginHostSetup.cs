using System.Globalization;
using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.DependencyInjection;
using JarvisNet.Plugins.Sdk.Infrastructure;
using JarvisNet.Plugins.Sdk.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JarvisNet.SearchWeb;

public sealed class SearchWebPluginHostSetup : IJarvisPluginHostSetup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddJarvisPluginTurnHandler<SearchWebPluginTurnHandler>();
        services.AddHttpClient(
            DuckDuckGoSearchServiceRegistration.HttpClientName,
            static client => DuckDuckGoSearchServiceRegistration.ConfigureClient(client));
        services.AddHttpClient(
            GoogleNewsRssSearchServiceRegistration.HttpClientName,
            static client => GoogleNewsRssSearchServiceRegistration.ConfigureClient(client));

        services.AddSingleton(sp =>
        {
            var culture = ResolveSearchCulture(sp);
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var newsClient = GoogleNewsRssSearchServiceRegistration.ConfigureClient(
                factory.CreateClient(GoogleNewsRssSearchServiceRegistration.HttpClientName));
            var newsSearch = new GoogleNewsRssSearchService(
                newsClient,
                loggerFactory.CreateLogger<GoogleNewsRssSearchService>(),
                culture);
            var ddgClient = DuckDuckGoSearchServiceRegistration.ConfigureClient(
                factory.CreateClient(DuckDuckGoSearchServiceRegistration.HttpClientName));
            return new DuckDuckGoSearchService(
                ddgClient,
                loggerFactory.CreateLogger<DuckDuckGoSearchService>(),
                culture,
                newsSearch);
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
