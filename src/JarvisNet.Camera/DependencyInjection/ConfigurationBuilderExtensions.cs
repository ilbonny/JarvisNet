using JarvisNet.Engine.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace JarvisNet.Camera.DependencyInjection;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddJarvisNetSharedCameraConfiguration(
        this IConfigurationBuilder configuration,
        string contentRootPath)
    {
        var repositoryRoot = RepositoryRootLocator.Find(contentRootPath);
        var configDir = Path.Combine(repositoryRoot, "config");

        var sharedPath = Path.Combine(configDir, "jarvisnet.camera.json");
        if (File.Exists(sharedPath))
        {
            configuration.AddJsonFile(sharedPath, optional: true, reloadOnChange: true);
        }

        var secretsPath = Path.Combine(configDir, "jarvisnet.camera.secrets.json");
        if (File.Exists(secretsPath))
        {
            configuration.AddJsonFile(secretsPath, optional: true, reloadOnChange: true);
        }

        return configuration;
    }
}
