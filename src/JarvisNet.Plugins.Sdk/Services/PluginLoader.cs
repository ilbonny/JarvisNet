using System.Reflection;
using System.Runtime.Loader;
using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.Attributes;
using JarvisNet.Plugins.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace JarvisNet.Plugins.Sdk.Services;

public sealed class PluginLoader
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginLoader> _logger;
    private IReadOnlyList<PluginDescriptor> _loadedDescriptors = [];

    public PluginLoader(IServiceProvider serviceProvider, ILogger<PluginLoader> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IReadOnlyList<PluginDescriptor> GetLoadedDescriptors() => _loadedDescriptors;

    public IReadOnlyList<PluginDescriptor> LoadAndRegisterPlugins(
        Kernel kernel,
        string? pluginsFolder = null)
    {
        pluginsFolder ??= Path.Combine(AppContext.BaseDirectory, "Plugins");
        var descriptors = new List<PluginDescriptor>();

        if (!Directory.Exists(pluginsFolder))
        {
            _logger.LogInformation("Cartella plugin non trovata ({PluginsFolder}); nessun plugin caricato.", pluginsFolder);
            _loadedDescriptors = descriptors;
            return _loadedDescriptors;
        }

        var dllPaths = Directory.EnumerateFiles(pluginsFolder, "*.dll", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var dllPath in dllPaths)
        {
            var fileName = Path.GetFileName(dllPath);
            if (fileName.StartsWith("JarvisNet.Plugins.Sdk", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
                RegisterPluginTypesFromAssembly(assembly, kernel, descriptors, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Impossibile caricare il plugin da {PluginAssembly}.", dllPath);
            }
        }

        _loadedDescriptors = descriptors;
        _logger.LogInformation("Plugin caricati: {Count}.", descriptors.Count);
        return _loadedDescriptors;
    }

    private void RegisterPluginTypesFromAssembly(
        Assembly assembly,
        Kernel kernel,
        List<PluginDescriptor> descriptors,
        string fileName)
    {
        foreach (var type in assembly.GetExportedTypes())
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            var attribute = type.GetCustomAttribute<JarvisPluginAttribute>();
            if (attribute is null)
            {
                continue;
            }

            if (!typeof(IJarvisPlugin).IsAssignableFrom(type))
            {
                _logger.LogWarning(
                    "Tipo {PluginType} in {PluginAssembly} ha JarvisPluginAttribute ma non implementa IJarvisPlugin.",
                    type.FullName,
                    fileName);
                continue;
            }

            try
            {
                var instance = ActivatorUtilities.CreateInstance(_serviceProvider, type);
                kernel.ImportPluginFromObject(instance, attribute.Name);
                descriptors.Add(new PluginDescriptor(attribute.Name, attribute.Version, attribute.Description));
                _logger.LogInformation(
                    "Plugin registrato: {PluginName} v{PluginVersion} ({PluginDescription}).",
                    attribute.Name,
                    attribute.Version,
                    attribute.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'istanziazione del plugin {PluginType}.", type.FullName);
            }
        }
    }
}
