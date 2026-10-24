using System.Text.RegularExpressions;

namespace JarvisNet.Engine.Infrastructure;

public static partial class McpPluginNaming
{
    public static string SanitizePluginName(string serverKey)
    {
        var sanitized = InvalidPluginNameChars().Replace(serverKey, "_").Trim('_');
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "McpServer";
        }

        if (!char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized = "Mcp_" + sanitized;
        }

        return sanitized;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9_]", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidPluginNameChars();
}
