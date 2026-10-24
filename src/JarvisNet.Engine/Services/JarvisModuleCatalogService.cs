using JarvisNet.Engine.Infrastructure;
using JarvisNet.Engine.Models;
using JarvisNet.Engine.Options;
using JarvisNet.Plugins.Sdk.Services;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace JarvisNet.Engine.Services;

public sealed class JarvisModuleCatalogService
{
    private readonly Kernel _kernel;
    private readonly McpOptions _mcpOptions;
    private readonly PluginLoader _pluginLoader;

    public JarvisModuleCatalogService(
        Kernel kernel,
        IOptions<McpOptions> mcpOptions,
        PluginLoader pluginLoader)
    {
        _kernel = kernel;
        _mcpOptions = mcpOptions.Value;
        _pluginLoader = pluginLoader;
    }

    public event EventHandler? CatalogChanged;

    public void NotifyCatalogChanged() => CatalogChanged?.Invoke(this, EventArgs.Empty);

    public IReadOnlyList<JarvisModuleInfo> GetModules()
    {
        var mcpPluginNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (serverKey, config) in _mcpOptions.Servers)
        {
            if (!config.Enabled)
            {
                continue;
            }

            mcpPluginNames.Add(McpPluginNaming.SanitizePluginName(serverKey));
        }

        var descriptorByName = _pluginLoader
            .GetLoadedDescriptors()
            .ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);

        var modules = new List<JarvisModuleInfo>();
        foreach (var collection in _kernel.Plugins.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            var kind = mcpPluginNames.Contains(collection.Name) ? "mcp" : "plugin";
            var description = descriptorByName.TryGetValue(collection.Name, out var descriptor)
                ? descriptor.Description
                : kind == "mcp"
                    ? "MCP server"
                    : string.Empty;

            modules.Add(new JarvisModuleInfo(
                collection.Name,
                kind,
                collection.Count(),
                description));
        }

        return modules;
    }
}
