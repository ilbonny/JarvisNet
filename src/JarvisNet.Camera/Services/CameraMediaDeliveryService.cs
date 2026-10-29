using JarvisNet.Camera.Models;
using JarvisNet.Camera.Options;
using JarvisNet.Core;
using JarvisNet.Telegram.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace JarvisNet.Camera.Services;

public sealed class CameraMediaDeliveryService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IOptionsMonitor<TelegramOptions> _telegram;
    private readonly IOptionsMonitor<CameraOptions> _camera;
    private readonly CameraMediaCache _mediaCache;
    private readonly ILogger<CameraMediaDeliveryService> _logger;

    public CameraMediaDeliveryService(
        ITelegramBotClient botClient,
        IOptionsMonitor<TelegramOptions> telegram,
        IOptionsMonitor<CameraOptions> camera,
        CameraMediaCache mediaCache,
        ILogger<CameraMediaDeliveryService> logger)
    {
        _botClient = botClient;
        _telegram = telegram;
        _camera = camera;
        _mediaCache = mediaCache;
        _logger = logger;
    }

    public async Task DeliverForSessionAsync(
        string sessionKey,
        IReadOnlyList<CameraTurnMediaItem> items,
        Func<CameraTurnMediaItem, string, CancellationToken, Task>? uiNotifier,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            if (TryParseTelegramChatId(sessionKey, out var chatId))
            {
                await SendToTelegramAsync(chatId, item, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (string.Equals(sessionKey, JarvisChatSessionKeys.Voice, StringComparison.Ordinal)
                && uiNotifier is not null)
            {
                var contentType = item.Kind == CameraMediaKind.Photo ? "image/jpeg" : "video/x-msvideo";
                var mediaId = _mediaCache.Store(item.Data, contentType, item.FileName);
                var url = $"/api/camera/media/{mediaId}";
                await uiNotifier(item, url, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await SendToDefaultTelegramChatsAsync(item, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SendToTelegramAsync(long chatId, CameraTurnMediaItem item, CancellationToken cancellationToken)
    {
        var telegram = _telegram.CurrentValue;
        if (!telegram.IsConfigured || !telegram.IsChatAllowed(chatId))
        {
            _logger.LogWarning("Skip Telegram media delivery: chat {ChatId} not allowed.", chatId);
            return;
        }

        await using var stream = new MemoryStream(item.Data, writable: false);
        var input = InputFile.FromStream(stream, item.FileName);
        if (item.Kind == CameraMediaKind.Photo)
        {
            await _botClient.SendPhoto(chatId, input, caption: item.Caption, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _botClient.SendVideo(chatId, input, caption: item.Caption, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task SendToDefaultTelegramChatsAsync(CameraTurnMediaItem item, CancellationToken cancellationToken)
    {
        var camera = _camera.CurrentValue;
        var telegram = _telegram.CurrentValue;
        if (!camera.Enabled || !telegram.IsConfigured)
        {
            return;
        }

        var chatIds = camera.NotifyChatIds.Length > 0 ? camera.NotifyChatIds : telegram.AllowedChatIds;
        foreach (var chatId in chatIds)
        {
            if (!telegram.IsChatAllowed(chatId))
            {
                continue;
            }

            await SendToTelegramAsync(chatId, item, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool TryParseTelegramChatId(string sessionKey, out long chatId)
    {
        chatId = 0;
        const string prefix = "telegram:";
        if (!sessionKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return long.TryParse(
            sessionKey[prefix.Length..],
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out chatId);
    }
}
