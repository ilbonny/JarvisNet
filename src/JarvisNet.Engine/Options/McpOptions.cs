namespace JarvisNet.Engine.Options;

public sealed class McpServerConfig
{
    public string Command { get; set; } = string.Empty;

    public string[] Args { get; set; } = [];

    public bool Enabled { get; set; } = true;

    public Dictionary<string, string> Env { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class McpOptions
{
    public const string SectionName = "McpServers";

    public Dictionary<string, McpServerConfig> Servers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
