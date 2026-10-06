using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.Infrastructure;
using JarvisNet.Plugins.Sdk.Options;
using JarvisNet.Plugins.Sdk.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JarvisNet.Plugins.Sdk.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJarvisPlugins(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PluginOptions>(configuration.GetSection(PluginOptions.SectionName));
        var pluginOptions = configuration.GetSection(PluginOptions.SectionName).Get<PluginOptions>()
            ?? new PluginOptions();
        services.AddJarvisPluginHostConfiguration(PluginFolderResolver.Resolve(pluginOptions));
        return services.AddJarvisPluginLoader();
    }

    public static IServiceCollection AddJarvisPluginLoader(this IServiceCollection services)
    {
        services.AddSingleton<PluginLoader>();
        return services;
    }

    public static IServiceCollection AddJarvisPluginTurnHandler<THandler>(this IServiceCollection services)
        where THandler : class, IJarvisPluginTurnHandler
    {
        services.AddSingleton<IJarvisPluginTurnHandler, THandler>();
        return services;
    }

    /// <param name="pluginsFolder">Cartella plugin; se null usa <c>Plugins</c> sotto la base directory dell'app.</param>
    public static IServiceCollection AddJarvisPluginHostConfiguration(
        this IServiceCollection services,
        string? pluginsFolder = null)
    {
        pluginsFolder ??= Path.Combine(AppContext.BaseDirectory, "Plugins");
        PluginHostConfigurator.ConfigureServices(services, pluginsFolder);
        return services;
    }
}
