using JarvisNet.Audio.Internal;
using Microsoft.Extensions.Configuration;

namespace JarvisNet.Audio.DependencyInjection;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddJarvisNetSharedAudioConfiguration(
        this IConfigurationBuilder configuration,
        string contentRootPath,
        string? profile = null)
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot(contentRootPath);
        profile ??= Environment.GetEnvironmentVariable("JARVISNET_AUDIO_PROFILE");
        var fileName = string.IsNullOrWhiteSpace(profile)
            ? "jarvisnet.audio.json"
            : $"jarvisnet.audio.{profile.Trim()}.json";
        var sharedPath = Path.Combine(repositoryRoot, "config", fileName);
        if (File.Exists(sharedPath))
        {
            configuration.AddJsonFile(sharedPath, optional: false, reloadOnChange: true);
        }
        else if (!string.IsNullOrWhiteSpace(profile))
        {
            throw new FileNotFoundException(
                $"Audio config profile '{profile}' not found at '{sharedPath}'.",
                sharedPath);
        }

        return configuration;
    }
}
