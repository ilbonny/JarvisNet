namespace JarvisNet.Audio.Internal;

internal static class SpeechTranscriptFilter
{
    public static bool IsMeaningfulFinalTranscript(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length < 2)
        {
            return false;
        }

        return trimmed.Any(char.IsLetterOrDigit);
    }
}
