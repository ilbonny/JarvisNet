using JarvisNet.Audio.Abstractions;
using JarvisNet.Core;
using JarvisNet.Engine.Services;
using JarvisNet.Telegram.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace JarvisNet.Telegram.Services;

internal sealed class JarvisTelegramUpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly OllamaBrainService _brain;
    private readonly ISpeechToTextBatchTranscriber _batchTranscriber;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly ILogger<JarvisTelegramUpdateHandler> _logger;

    public JarvisTelegramUpdateHandler(
        ITelegramBotClient botClient,
        OllamaBrainService brain,
        ISpeechToTextBatchTranscriber batchTranscriber,
        IOptionsMonitor<TelegramOptions> options,
        ILogger<JarvisTelegramUpdateHandler> logger)
    {
        _botClient = botClient;
        _brain = brain;
        _batchTranscriber = batchTranscriber;
        _options = options;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not Message message || message.Chat.Id == 0)
        {
            return;
        }

        var chatId = message.Chat.Id;
        if (!await EnsureChatAllowedAsync(chatId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (message.Voice is not null)
        {
            await HandleVoiceMessageAsync(chatId, message.Voice, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (message.Text is not { } text)
        {
            return;
        }

        var normalized = text.Trim();
        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            if (await TryHandleCommandAsync(chatId, normalized, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        await ProcessUserTextAsync(chatId, normalized, cancellationToken).ConfigureAwait(false);
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Errore polling Telegram.");
        return Task.CompletedTask;
    }

    private async Task HandleVoiceMessageAsync(
        long chatId,
        Voice voice,
        CancellationToken cancellationToken)
    {
        await _botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string? transcript;
        try
        {
            var file = await _botClient.GetFile(voice.FileId, cancellationToken).ConfigureAwait(false);
            await using var audioStream = new MemoryStream();
            await _botClient.DownloadFile(file, audioStream, cancellationToken).ConfigureAwait(false);
            audioStream.Position = 0;
            transcript = await _batchTranscriber
                .TranscribeOggOpusAsync(audioStream, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore trascrizione vocale Telegram per chat {ChatId}.", chatId);
            await _botClient.SendMessage(
                    chatId,
                    "Non sono riuscito a trascrivere il messaggio vocale. Riprova o scrivi in testo.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            await _botClient.SendMessage(
                    chatId,
                    "Non ho capito l'audio (troppo corto o silenzioso). Puoi ripetere o scrivere in testo?",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        _logger.LogInformation("Telegram voce ({ChatId}) → testo: {Text}", chatId, transcript);
        await ProcessUserTextAsync(chatId, transcript, cancellationToken, quotedTranscript: transcript)
            .ConfigureAwait(false);
    }

    private async Task ProcessUserTextAsync(
        long chatId,
        string userText,
        CancellationToken cancellationToken,
        string? quotedTranscript = null)
    {
        _logger.LogInformation("Telegram utente ({ChatId}): {Text}", chatId, userText);

        await _botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var sessionKey = JarvisChatSessionKeys.TelegramChat(chatId);
        string reply;
        try
        {
            reply = await _brain.SendAsync(userText, sessionKey, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore risposta Telegram per chat {ChatId}.", chatId);
            reply = "Si è verificato un errore durante l'elaborazione. Riprova tra poco.";
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            reply = "(nessuna risposta)";
        }

        _logger.LogInformation("Telegram Jarvis ({ChatId}): {Reply}", chatId, reply);

        if (!string.IsNullOrWhiteSpace(quotedTranscript))
        {
            var prefix = $"🎤 Ho capito: «{quotedTranscript}»\n\n";
            reply = prefix + reply;
        }

        foreach (var part in TelegramOutboundMessageSplitter.Split(reply))
        {
            await _botClient.SendMessage(chatId, part, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<bool> EnsureChatAllowedAsync(long chatId, CancellationToken cancellationToken)
    {
        if (_options.CurrentValue.IsChatAllowed(chatId))
        {
            return true;
        }

        await _botClient.SendMessage(
                chatId,
                $"Chat non autorizzato. Il tuo chat id è {chatId}: aggiungilo in config/jarvisnet.telegram.secrets.json (AllowedChatIds).",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return false;
    }

    private async Task<bool> TryHandleCommandAsync(long chatId, string command, CancellationToken cancellationToken)
    {
        var firstToken = command.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var cmd = firstToken.ToLowerInvariant();

        switch (cmd)
        {
            case "/start":
                await _botClient.SendMessage(
                        chatId,
                        "Ciao! Sono JarvisNet su Telegram. Scrivimi in testo o invia un messaggio vocale: userò gli stessi strumenti della UI (ricerca web, MCP, ecc.).\n\n"
                        + "Comandi:\n"
                        + "/reset — azzera la cronologia di questa chat\n"
                        + $"/chatid — mostra il tuo chat id ({chatId})",
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case "/reset":
            case "/nuova":
                _brain.ClearHistory(JarvisChatSessionKeys.TelegramChat(chatId));
                await _botClient.SendMessage(
                        chatId,
                        "Cronologia di questa chat azzerata.",
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case "/chatid":
                await _botClient.SendMessage(
                        chatId,
                        $"Chat id: {chatId}",
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }
}
