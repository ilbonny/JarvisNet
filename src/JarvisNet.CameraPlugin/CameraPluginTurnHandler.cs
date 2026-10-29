using JarvisNet.Camera.Abstractions;
using JarvisNet.Camera.Services;
using JarvisNet.Plugins.Sdk;
using JarvisNet.Plugins.Sdk.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace JarvisNet.CameraPlugin;

public sealed class CameraPluginTurnHandler : IJarvisPluginTurnHandler
{
    private readonly ICameraTurnMediaBuffer _mediaBuffer;
    private readonly CameraMediaDeliveryService _delivery;
    private readonly IJarvisCameraUiNotifier? _uiNotifier;
    private readonly ILogger<CameraPluginTurnHandler> _logger;

    public CameraPluginTurnHandler(
        ICameraTurnMediaBuffer mediaBuffer,
        CameraMediaDeliveryService delivery,
        ILogger<CameraPluginTurnHandler> logger,
        IJarvisCameraUiNotifier? uiNotifier = null)
    {
        _mediaBuffer = mediaBuffer;
        _delivery = delivery;
        _uiNotifier = uiNotifier;
        _logger = logger;
    }

    public int Order => 5;

    public async Task<JarvisPluginTurnOutcome?> TryEnhanceTurnAsync(
        JarvisPluginTurnContext context,
        CancellationToken cancellationToken)
    {
        JarvisPluginTurnOutcome? directOutcome = null;
        if (context.TurnScope.ToolsInvokedThisTurn == 0
            && CameraAssist.TryParseDirectAction(
                context.UserMessage,
                out var action,
                out var deviceQuery,
                out var videoSeconds))
        {
            _logger.LogInformation(
                "Camera request without tool call; running direct action {Action} for device «{Device}».",
                action,
                string.IsNullOrWhiteSpace(deviceQuery) ? "(default)" : deviceQuery);

            var directText = await InvokeDirectActionAsync(
                    context,
                    action,
                    deviceQuery,
                    videoSeconds,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(directText))
            {
                directOutcome = new JarvisPluginTurnOutcome(directText);
            }
        }

        var pending = _mediaBuffer.TakeAll();
        if (pending.Count > 0)
        {
            _logger.LogInformation(
                "Delivering {Count} camera media item(s) for session {Session}.",
                pending.Count,
                context.TurnScope.CurrentSessionKey);

            await _delivery.DeliverForSessionAsync(
                    context.TurnScope.CurrentSessionKey,
                    pending,
                    _uiNotifier is null
                        ? null
                        : (item, url, ct) => _uiNotifier.NotifyMediaAsync(item, url, ct),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return directOutcome;
    }

    private async Task<string?> InvokeDirectActionAsync(
        JarvisPluginTurnContext context,
        CameraAssist.CameraDirectAction action,
        string deviceQuery,
        int videoSeconds,
        CancellationToken cancellationToken)
    {
        if (!context.Kernel.Plugins.TryGetPlugin(CameraPluginMetadata.PluginName, out _))
        {
            _logger.LogWarning("Camera plugin is not registered; direct camera action skipped.");
            return null;
        }

        var (functionName, arguments) = action switch
        {
            CameraAssist.CameraDirectAction.ListDevices => (
                CameraPluginMetadata.ListDevicesFunction,
                new KernelArguments()),
            CameraAssist.CameraDirectAction.CapturePhoto => (
                CameraPluginMetadata.CapturePhotoFunction,
                new KernelArguments { ["deviceNameOrId"] = deviceQuery }),
            CameraAssist.CameraDirectAction.CaptureVideo => (
                CameraPluginMetadata.CaptureVideoFunction,
                new KernelArguments
                {
                    ["deviceNameOrId"] = deviceQuery,
                    ["durationSeconds"] = videoSeconds,
                }),
            CameraAssist.CameraDirectAction.SetMotionOn => (
                CameraPluginMetadata.SetMotionDetectionFunction,
                new KernelArguments { ["deviceNameOrId"] = deviceQuery, ["enabled"] = true }),
            CameraAssist.CameraDirectAction.SetMotionOff => (
                CameraPluginMetadata.SetMotionDetectionFunction,
                new KernelArguments { ["deviceNameOrId"] = deviceQuery, ["enabled"] = false }),
            _ => (string.Empty, new KernelArguments()),
        };

        if (string.IsNullOrEmpty(functionName))
        {
            return null;
        }

        try
        {
            var result = await context.Kernel
                .InvokeAsync(
                    CameraPluginMetadata.PluginName,
                    functionName,
                    arguments,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return result.GetValue<string>()?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Direct camera action {Action} failed.", action);
            return "Non sono riuscito a completare l'operazione sulla telecamera. Riprova tra poco.";
        }
    }
}
