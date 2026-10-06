using JarvisNet.Plugins.Sdk.Options;

namespace JarvisNet.Plugins.Sdk.Infrastructure;

public static class PluginFolderResolver
{
    private const string SolutionMarker = "JarvisNet.slnx";

    public static string Resolve(PluginOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Folder))
        {
            return Path.GetFullPath(options.Folder);
        }

        var outputPlugins = Path.Combine(AppContext.BaseDirectory, "Plugins");
        if (DirectoryContainsPluginAssemblies(outputPlugins))
        {
            return outputPlugins;
        }

        var repositoryPlugins = Path.Combine(FindRepositoryRoot(AppContext.BaseDirectory), "Plugins");
        return Directory.Exists(repositoryPlugins) ? repositoryPlugins : outputPlugins;
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

    private static bool DirectoryContainsPluginAssemblies(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        return Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)
            .Any(path =>
            {
                var name = Path.GetFileName(path);
                return !name.StartsWith("JarvisNet.Plugins.Sdk", StringComparison.OrdinalIgnoreCase);
            });
    }
}
