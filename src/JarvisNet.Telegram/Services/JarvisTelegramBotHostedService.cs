using JarvisNet.Telegram.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace JarvisNet.Telegram.Services;

internal sealed class JarvisTelegramBotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly JarvisTelegramUpdateHandler _updateHandler;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly ILogger<JarvisTelegramBotHostedService> _logger;

    public JarvisTelegramBotHostedService(
        ITelegramBotClient botClient,
        JarvisTelegramUpdateHandler updateHandler,
        IOptionsMonitor<TelegramOptions> options,
        ILogger<JarvisTelegramBotHostedService> logger)
    {
        _botClient = botClient;
        _updateHandler = updateHandler;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.IsConfigured)
        {
            _logger.LogInformation(
                "Bot Telegram disabilitato (Enabled=false o BotToken mancante). Vedi config/jarvisnet.telegram.json.");
            return;
        }

        var me = await _botClient.GetMe(stoppingToken).ConfigureAwait(false);
        _logger.LogInformation("Bot Telegram avviato: @{Username}", me.Username);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message],
        };

        _botClient.StartReceiving(
            _updateHandler.HandleUpdateAsync,
            _updateHandler.HandleErrorAsync,
            receiverOptions,
            stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Bot Telegram in arresto.");
        }
    }
}
