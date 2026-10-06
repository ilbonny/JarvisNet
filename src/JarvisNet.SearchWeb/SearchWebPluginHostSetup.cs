using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

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
    }
}
