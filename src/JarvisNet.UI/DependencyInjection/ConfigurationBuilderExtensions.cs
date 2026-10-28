using JarvisNet.Engine.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace JarvisNet.UI.DependencyInjection;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddJarvisNetSharedSchedulesConfiguration(
        this IConfigurationBuilder configuration,
        string contentRootPath)
    {
        var repositoryRoot = RepositoryRootLocator.Find(contentRootPath);
        var sharedPath = Path.Combine(repositoryRoot, "config", "jarvisnet.schedules.json");
        if (File.Exists(sharedPath))
        {
            configuration.AddJsonFile(sharedPath, optional: true, reloadOnChange: true);
        }

        return configuration;
    }
}
