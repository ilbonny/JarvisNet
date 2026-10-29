using JarvisNet.Camera.Options;
using JarvisNet.Telegram.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace JarvisNet.Camera.Services;

public sealed class CameraMotionAlertService
{
    private readonly ITelegramBotClient _botClient;
    private readonly CameraVisionCaptionService _vision;
    private readonly IOptionsMonitor<CameraOptions> _camera;
    private readonly IOptionsMonitor<TelegramOptions> _telegram;
    private readonly ILogger<CameraMotionAlertService> _logger;
    private readonly object _cooldownLock = new();
    private DateTimeOffset _lastAlertUtc = DateTimeOffset.MinValue;

    public CameraMotionAlertService(
        ITelegramBotClient botClient,
        CameraVisionCaptionService vision,
        IOptionsMonitor<CameraOptions> camera,
        IOptionsMonitor<TelegramOptions> telegram,
        ILogger<CameraMotionAlertService> logger)
    {
        _botClient = botClient;
        _vision = vision;
        _camera = camera;
        _telegram = telegram;
        _logger = logger;
    }

    public async Task<(bool Accepted, string? Error)> TrySendMotionPhotoAsync(
        Stream imageStream,
        string fileName,
        string? captionOverride,
        CancellationToken cancellationToken)
    {
        var cameraOptions = _camera.CurrentValue;
        if (!cameraOptions.Enabled)
        {
            return (false, "Camera ingest disabled (JarvisNet:Camera:Enabled=false).");
        }

        var telegramOptions = _telegram.CurrentValue;
        if (!telegramOptions.IsConfigured)
        {
            return (false, "Telegram bot not configured. Enable JarvisNet:Telegram and set BotToken.");
        }

        var chatIds = ResolveNotifyChatIds(cameraOptions, telegramOptions);
        if (chatIds.Length == 0)
        {
            return (false, "No Telegram chat ids configured (JarvisNet:Camera:NotifyChatIds or Telegram:AllowedChatIds).");
        }

        if (IsInCooldown(cameraOptions.MinSecondsBetweenAlerts))
        {
            _logger.LogDebug("Motion photo ignored (cooldown).");
            return (false, "Cooldown active; wait before sending another alert.");
        }

        var caption = string.IsNullOrWhiteSpace(captionOverride)
            ? await BuildCaptionAsync(imageStream, cameraOptions, cancellationToken).ConfigureAwait(false)
            : captionOverride.Trim();

        var sentCount = 0;
        foreach (var chatId in chatIds)
        {
            if (!telegramOptions.IsChatAllowed(chatId))
            {
                _logger.LogWarning("Skipping notify chat {ChatId}: not in Telegram AllowedChatIds.", chatId);
                continue;
            }

            imageStream.Position = 0;
            var input = InputFile.FromStream(imageStream, fileName);
            await _botClient.SendPhoto(
                    chatId: chatId,
                    photo: input,
                    caption: caption,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            sentCount++;
            _logger.LogInformation("Motion photo sent to Telegram chat {ChatId}.", chatId);
        }

        if (sentCount == 0)
        {
            return (false, "No authorized Telegram chat received the photo.");
        }

        MarkAlertSent();
        return (true, null);
    }

    private async Task<string> BuildCaptionAsync(
        Stream imageStream,
        CameraOptions cameraOptions,
        CancellationToken cancellationToken)
    {
        if (!cameraOptions.VisionAnalysisEnabled)
        {
            return cameraOptions.MotionCaption;
        }

        imageStream.Position = 0;
        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        var jpegBytes = ms.ToArray();

        var description = await _vision.TryDescribeImageAsync(jpegBytes, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(description))
        {
            _logger.LogInformation(
                "Using fallback MotionCaption (vision off or Ollama returned no text).");
            return cameraOptions.MotionCaption;
        }

        try
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, cameraOptions.VisionCaptionFormat, description);
        }
        catch (FormatException)
        {
            return $"{cameraOptions.MotionCaption} {description}";
        }
    }

    private static long[] ResolveNotifyChatIds(CameraOptions camera, TelegramOptions telegram) =>
        camera.NotifyChatIds.Length > 0
            ? camera.NotifyChatIds
            : telegram.AllowedChatIds;

    private bool IsInCooldown(int minSecondsBetweenAlerts)
    {
        if (minSecondsBetweenAlerts <= 0)
        {
            return false;
        }

        lock (_cooldownLock)
        {
            var elapsed = DateTimeOffset.UtcNow - _lastAlertUtc;
            return elapsed.TotalSeconds < minSecondsBetweenAlerts;
        }
    }

    private void MarkAlertSent()
    {
        lock (_cooldownLock)
        {
            _lastAlertUtc = DateTimeOffset.UtcNow;
        }
    }
}
