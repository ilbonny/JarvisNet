namespace JarvisNet.Telegram.Services;

internal static class TelegramOutboundMessageSplitter
{
    public const int MaxMessageLength = 4096;

    public static IEnumerable<string> Split(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        if (text.Length <= MaxMessageLength)
        {
            yield return text;
            yield break;
        }

        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(MaxMessageLength, text.Length - start);
            yield return text.Substring(start, length);
            start += length;
        }
    }
}
