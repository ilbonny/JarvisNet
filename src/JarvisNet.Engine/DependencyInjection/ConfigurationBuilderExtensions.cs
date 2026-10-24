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

        AddJarvisNetMcpConfigurationFiles(configuration, repositoryRoot);

        return configuration;
    }

    public static IConfigurationBuilder AddJarvisNetSharedMcpConfiguration(
        this IConfigurationBuilder configuration,
        string contentRootPath)
    {
        var repositoryRoot = RepositoryRootLocator.Find(contentRootPath);
        AddJarvisNetMcpConfigurationFiles(configuration, repositoryRoot);

        return configuration;
    }

    private static void AddJarvisNetMcpConfigurationFiles(
        IConfigurationBuilder configuration,
        string repositoryRoot)
    {
        var configDir = Path.Combine(repositoryRoot, "config");
        var mcpPath = Path.Combine(configDir, "jarvisnet.mcp.json");
        if (File.Exists(mcpPath))
        {
            configuration.AddJsonFile(mcpPath, optional: true, reloadOnChange: true);
        }

        var mcpSecretsPath = Path.Combine(configDir, "jarvisnet.mcp.secrets.json");
        if (File.Exists(mcpSecretsPath))
        {
            configuration.AddJsonFile(mcpSecretsPath, optional: true, reloadOnChange: true);
        }

        var mcpSecretsEnvPath = Path.Combine(configDir, "jarvisnet.mcp.secrets.env");
        if (File.Exists(mcpSecretsEnvPath))
        {
            configuration.AddInMemoryCollection(ParseEnvFile(mcpSecretsEnvPath));
        }
    }

    private static IEnumerable<KeyValuePair<string, string?>> ParseEnvFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim().Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);
            var value = line[(eq + 1)..].Trim();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            yield return new KeyValuePair<string, string?>(key, value);
        }
    }
}
