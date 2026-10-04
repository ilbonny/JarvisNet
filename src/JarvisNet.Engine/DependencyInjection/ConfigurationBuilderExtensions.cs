using Microsoft.Extensions.Configuration;

namespace JarvisNet.Engine.DependencyInjection;

public static class ConfigurationBuilderExtensions
{
    private const string SolutionMarker = "JarvisNet.slnx";

    public static IConfigurationBuilder AddJarvisNetSharedEngineConfiguration(
        this IConfigurationBuilder configuration,
        string contentRootPath)
    {
        var repositoryRoot = FindRepositoryRoot(contentRootPath);
        var sharedPath = Path.Combine(repositoryRoot, "config", "jarvisnet.engine.json");
        if (File.Exists(sharedPath))
        {
            configuration.AddJsonFile(sharedPath, optional: false, reloadOnChange: true);
        }

        return configuration;
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionMarker)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(startDirectory);
    }
}
