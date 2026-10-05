namespace JarvisNet.Engine.Infrastructure;

/// <summary>
/// Ensures MCP stdio servers (npx) run with Node 20+ when launched from dotnet (often Node 18 on PATH).
/// </summary>
internal static class NodeToolchainResolver
{
    public static string ResolveExecutable(string command)
    {
        if (!string.Equals(command, "npx", StringComparison.OrdinalIgnoreCase))
        {
            return command;
        }

        foreach (var nodeDir in GetCandidateNodeDirectories())
        {
            var npx = Path.Combine(nodeDir, OperatingSystem.IsWindows() ? "npx.cmd" : "npx");
            if (File.Exists(npx))
            {
                return npx;
            }
        }

        return command;
    }

    public static void PrependNodeToPath(IDictionary<string, string?> environment)
    {
        foreach (var nodeDir in GetCandidateNodeDirectories())
        {
            if (!File.Exists(Path.Combine(nodeDir, "node.exe")))
            {
                continue;
            }

            environment.TryGetValue("PATH", out var currentPath);
            currentPath ??= Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (currentPath.Contains(nodeDir, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            environment["PATH"] = nodeDir + Path.PathSeparator + currentPath;
            return;
        }
    }

    private static IEnumerable<string> GetCandidateNodeDirectories()
    {
        var nvmHome = Environment.GetEnvironmentVariable("NVM_HOME");
        if (!string.IsNullOrWhiteSpace(nvmHome))
        {
            yield return Path.Combine(nvmHome, "nodejs");
        }

        if (OperatingSystem.IsWindows())
        {
            yield return @"C:\nvm4w\nodejs";
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Path.Combine(programFiles, "nodejs");
    }
}
