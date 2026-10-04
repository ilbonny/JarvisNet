using JarvisNet.Audio.Internal;
using Microsoft.Extensions.Configuration;

namespace JarvisNet.Audio.DependencyInjection;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddJarvisNetSharedAudioConfiguration(
        this IConfigurationBuilder configuration,
        string contentRootPath)
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot(contentRootPath);
        var sharedPath = Path.Combine(repositoryRoot, "config", "jarvisnet.audio.json");
        if (File.Exists(sharedPath))
        {
            configuration.AddJsonFile(sharedPath, optional: false, reloadOnChange: true);
        }

        return configuration;
    }
}
