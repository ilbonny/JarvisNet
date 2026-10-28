namespace JarvisNet.Engine.Infrastructure;

/// <summary>
/// Locates the official <c>github-mcp-server</c> binary under <c>github-mcp/</c> at repo root
/// (installed via <c>scripts/Download-GitHubMcp.ps1</c>).
/// </summary>
internal static class GitHubMcpResolver
{
    private const string InstallFolderName = "github-mcp";
    private const string ExecutableBaseName = "github-mcp-server";

    public static bool IsGitHubMcpCommand(string command) =>
        string.Equals(command, ExecutableBaseName, StringComparison.OrdinalIgnoreCase)
        || command.EndsWith($"{Path.DirectorySeparatorChar}{ExecutableBaseName}.exe", StringComparison.OrdinalIgnoreCase)
        || command.EndsWith($"/{ExecutableBaseName}.exe", StringComparison.OrdinalIgnoreCase)
        || command.EndsWith($"{Path.DirectorySeparatorChar}{ExecutableBaseName}", StringComparison.OrdinalIgnoreCase)
        || command.EndsWith($"/{ExecutableBaseName}", StringComparison.OrdinalIgnoreCase);

    public static string ResolveExecutable(string startDirectory)
    {
        var repoRoot = RepositoryRootLocator.Find(startDirectory);
        var installDir = Path.Combine(repoRoot, InstallFolderName);
        var fileName = OperatingSystem.IsWindows() ? $"{ExecutableBaseName}.exe" : ExecutableBaseName;
        return Path.Combine(installDir, fileName);
    }
}
