using Microsoft.Extensions.DependencyInjection;

namespace JarvisNet.Plugins.Sdk.Abstractions;

/// <summary>
/// Implementato dal plugin per registrare servizi host (es. HttpClient) prima del caricamento nel kernel.
/// </summary>
public interface IJarvisPluginHostSetup
{
    void ConfigureServices(IServiceCollection services);
}
