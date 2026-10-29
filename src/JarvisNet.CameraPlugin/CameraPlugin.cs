using System.ComponentModel;
using JarvisNet.Camera.Services;
using JarvisNet.Plugins.Sdk;
using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace JarvisNet.CameraPlugin;

[JarvisPlugin(
    CameraPluginMetadata.PluginName,
    "1.0.0",
    "Control ESP32-CAM devices: list cameras, capture photo/video, toggle motion detection.")]
public sealed class CameraPlugin : IJarvisPlugin
{
    private readonly CameraControlService _control;
    private readonly ILogger<CameraPlugin> _logger;

    public CameraPlugin(CameraControlService control, ILogger<CameraPlugin> logger)
    {
        _control = control;
        _logger = logger;
    }

    [KernelFunction(CameraPluginMetadata.ListDevicesFunction)]
    [Description(
        "List Jarvis-connected cameras (ESP32-CAM) with name, id, IP and online status. "
        + "Use when the user asks which devices or cameras are connected.")]
    public Task<string> ListConnectedDevicesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Camera.list_connected_devices invoked.");
        return Task.FromResult(_control.ListConnectedDevices());
    }

    [KernelFunction(CameraPluginMetadata.CapturePhotoFunction)]
    [Description(
        "Take a photo from a connected ESP32-CAM and deliver it on Telegram or the voice UI. "
        + "Use when the user asks to take/shoot a picture from a named camera.")]
    public Task<string> CapturePhotoAsync(
        [Description("Camera label (e.g. CAM1, CAM2) or hostname. Empty = first online camera.")]
        string deviceNameOrId = "",
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Camera.capture_photo invoked for {Device}.", deviceNameOrId);
        return _control.CapturePhotoAsync(deviceNameOrId, cancellationToken);
    }

    [KernelFunction(CameraPluginMetadata.CaptureVideoFunction)]
    [Description(
        "Record a short video clip (max 10 seconds) from a connected ESP32-CAM and send it. "
        + "Use when the user asks for a video from a camera.")]
    public Task<string> CaptureVideoAsync(
        [Description("Camera name or id")]
        string deviceNameOrId = "",
        [Description("Duration in seconds (1-10)")]
        int durationSeconds = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Camera.capture_video invoked for {Device}, {Seconds}s.",
            deviceNameOrId,
            durationSeconds);
        return _control.CaptureVideoAsync(deviceNameOrId, durationSeconds, cancellationToken);
    }

    [KernelFunction(CameraPluginMetadata.SetMotionDetectionFunction)]
    [Description(
        "Enable or disable automatic motion detection alerts on a camera. Default for new cameras is OFF. "
        + "Use when the user wants to turn motion detection on or off.")]
    public Task<string> SetMotionDetectionAsync(
        [Description("Camera name or id")]
        string deviceNameOrId = "",
        [Description("true to enable motion alerts, false to disable")]
        bool enabled = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Camera.set_motion_detection invoked for {Device}, enabled={Enabled}.",
            deviceNameOrId,
            enabled);
        return _control.SetMotionDetectionAsync(deviceNameOrId, enabled, cancellationToken);
    }
}
