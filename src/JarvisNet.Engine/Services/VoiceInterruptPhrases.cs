namespace JarvisNet.Engine.Services;

internal static class VoiceInterruptPhrases
{
    private static readonly string[] Phrases =
    [
        "stop",
        "basta",
        "fermati",
        "ferma",
        "silenzio",
        "zitto",
        "aspetta",
        "wait",
        "halt",
        "quiet",
        "stopp",
    ];

    public static bool IsInterruptCommand(string text)
    {
        var normalized = Normalize(text);
        if (normalized.Length == 0)
        {
            return false;
        }

        if (Phrases.Contains(normalized))
        {
            return true;
        }

        foreach (var phrase in Phrases)
        {
            if (normalized.StartsWith(phrase + " ", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string text) =>
        text.Trim().TrimEnd('.', '!', '?', ',', ';', ':').ToLowerInvariant();
}
