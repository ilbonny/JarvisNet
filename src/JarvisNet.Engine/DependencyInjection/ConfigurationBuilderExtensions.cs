using JarvisNet.Engine.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace JarvisNet.Engine.DependencyInjection;

public static class ConfigurationBuilderExtensions
{

    public static IConfigurationBuilder AddJarvisNetSharedEngineConfiguration(
        this IConfigurationBuilder configuration,
        string contentRootPath)
    {
        var repositoryRoot = RepositoryRootLocator.Find(contentRootPath);
        var sharedPath = Path.Combine(repositoryRoot, "config", "jarvisnet.engine.json");
        if (File.Exists(sharedPath))
        {
            configuration.AddJsonFile(sharedPath, optional: false, reloadOnChange: true);
        }

        var mcpPath = Path.Combine(repositoryRoot, "config", "jarvisnet.mcp.json");
        if (File.Exists(mcpPath))
        {
            configuration.AddJsonFile(mcpPath, optional: true, reloadOnChange: true);
        }

        return configuration;
    }

    public static IConfigurationBuilder AddJarvisNetSharedMcpConfiguration(
        this IConfigurationBuilder configuration,
        string contentRootPath)
    {
        var repositoryRoot = RepositoryRootLocator.Find(contentRootPath);
        var mcpPath = Path.Combine(repositoryRoot, "config", "jarvisnet.mcp.json");
        if (File.Exists(mcpPath))
        {
            configuration.AddJsonFile(mcpPath, optional: true, reloadOnChange: true);
        }

        return configuration;
    }
}
