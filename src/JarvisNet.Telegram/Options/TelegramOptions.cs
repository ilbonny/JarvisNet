namespace JarvisNet.Telegram.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "JarvisNet:Telegram";

    public bool Enabled { get; set; }

    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// When empty, any chat can talk to the bot (useful to discover your chat id via /start).
    /// When set, only listed chat ids are accepted.
    /// </summary>
    public long[] AllowedChatIds { get; set; } = [];

    public bool IsConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(BotToken);

    public bool IsChatAllowed(long chatId) =>
        AllowedChatIds.Length == 0 || AllowedChatIds.Contains(chatId);
}
