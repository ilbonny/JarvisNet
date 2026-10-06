using System.Reflection;
using System.Runtime.Loader;
using JarvisNet.Plugins.Sdk.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JarvisNet.Plugins.Sdk.Services;

public static class PluginHostConfigurator
{
    public static void ConfigureServices(
        IServiceCollection services,
        string pluginsFolder,
        ILogger? logger = null)
    {
        if (!Directory.Exists(pluginsFolder))
        {
            logger?.LogDebug(
                "Cartella plugin assente ({PluginsFolder}); nessuna configurazione host plugin.",
                pluginsFolder);
            return;
        }

        foreach (var dllPath in Directory.EnumerateFiles(pluginsFolder, "*.dll", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(dllPath);
            if (fileName.StartsWith("JarvisNet.Plugins.Sdk", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
                ApplyHostSetupFromAssembly(assembly, services, logger, fileName);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Impossibile configurare l'host dal plugin {PluginAssembly}.", dllPath);
            }
        }
    }

    private static void ApplyHostSetupFromAssembly(
        Assembly assembly,
        IServiceCollection services,
        ILogger? logger,
        string fileName)
    {
        foreach (var type in assembly.GetExportedTypes())
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            if (!typeof(IJarvisPluginHostSetup).IsAssignableFrom(type))
            {
                continue;
            }

            try
            {
                if (Activator.CreateInstance(type) is not IJarvisPluginHostSetup setup)
                {
                    continue;
                }

                setup.ConfigureServices(services);
                logger?.LogInformation(
                    "Configurazione host plugin applicata: {SetupType} ({PluginAssembly}).",
                    type.FullName,
                    fileName);
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    ex,
                    "Errore durante ConfigureServices del plugin host {SetupType}.",
                    type.FullName);
            }
        }
    }
}
