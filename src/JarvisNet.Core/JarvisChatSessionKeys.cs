namespace JarvisNet.Core;

public static class JarvisChatSessionKeys
{
    public const string Voice = "voice";

    public static string TelegramChat(long chatId) =>
        $"telegram:{chatId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}
