using JarvisNet.Engine.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace JarvisNet.Telegram.DependencyInjection;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddJarvisNetSharedTelegramConfiguration(
        this IConfigurationBuilder configuration,
        string contentRootPath)
    {
        var repositoryRoot = RepositoryRootLocator.Find(contentRootPath);
        var configDir = Path.Combine(repositoryRoot, "config");

        var sharedPath = Path.Combine(configDir, "jarvisnet.telegram.json");
        if (File.Exists(sharedPath))
        {
            configuration.AddJsonFile(sharedPath, optional: true, reloadOnChange: true);
        }

        var secretsPath = Path.Combine(configDir, "jarvisnet.telegram.secrets.json");
        if (File.Exists(secretsPath))
        {
            configuration.AddJsonFile(secretsPath, optional: true, reloadOnChange: true);
        }

        return configuration;
    }
}
