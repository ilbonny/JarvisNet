using JarvisNet.Camera.Models;
using JarvisNet.Camera.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JarvisNet.Camera.Services;

public sealed class CameraControlService
{
    private readonly ConnectedDeviceRegistry _registry;
    private readonly CameraDeviceHttpClient _deviceClient;
    private readonly CameraVisionCaptionService _vision;
    private readonly CameraMediaCache _mediaCache;
    private readonly ICameraTurnMediaBuffer _mediaBuffer;
    private readonly IOptionsMonitor<CameraOptions> _options;
    private readonly ILogger<CameraControlService> _logger;

    public CameraControlService(
        ConnectedDeviceRegistry registry,
        CameraDeviceHttpClient deviceClient,
        CameraVisionCaptionService vision,
        CameraMediaCache mediaCache,
        ICameraTurnMediaBuffer mediaBuffer,
        IOptionsMonitor<CameraOptions> options,
        ILogger<CameraControlService> logger)
    {
        _registry = registry;
        _deviceClient = deviceClient;
        _vision = vision;
        _mediaCache = mediaCache;
        _mediaBuffer = mediaBuffer;
        _options = options;
        _logger = logger;
    }

    public string ListConnectedDevices()
    {
        if (!_options.CurrentValue.Enabled)
        {
            return "Modulo telecamere disabilitato (JarvisNet:Camera:Enabled=false).";
        }

        var devices = _registry.GetSnapshots();
        if (devices.Count == 0)
        {
            return "Nessun dispositivo registrato. Accendi una ESP32-CAM sulla stessa rete e attendi l'heartbeat.";
        }

        var lines = devices.Select(d =>
        {
            var motion = d.MotionDetectionEnabled ? "movimento ON" : "movimento OFF";
            var host = string.IsNullOrWhiteSpace(d.HostLabel) ? d.Id : d.HostLabel;
            return $"- {d.DisplayName} (IP: {d.IpAddress}, host: {host}, {d.Status}, {motion})";
        });

        return "Dispositivi connessi:\n" + string.Join('\n', lines);
    }

    public async Task<string> CapturePhotoAsync(string deviceQuery, CancellationToken cancellationToken)
    {
        var device = ResolveDevice(deviceQuery);
        if (device is null)
        {
            return DescribeNotFound(deviceQuery);
        }

        if (!device.IsOnline)
        {
            return $"La telecamera «{device.DisplayName}» risulta offline.";
        }

        var jpeg = await _deviceClient.TryCapturePhotoAsync(device.IpAddress, cancellationToken)
            .ConfigureAwait(false);
        if (jpeg is null || jpeg.Length == 0)
        {
            return $"Non sono riuscito a scattare una foto da «{device.DisplayName}» ({device.IpAddress}).";
        }

        var caption = await BuildPhotoCaptionAsync(jpeg, device.DisplayName, cancellationToken)
            .ConfigureAwait(false);
        var fileName = SanitizeFileName(device.DisplayName) + ".jpg";

        _mediaBuffer.Enqueue(new CameraTurnMediaItem
        {
            Kind = CameraMediaKind.Photo,
            Data = jpeg,
            FileName = fileName,
            Caption = caption,
            DeviceName = device.DisplayName,
        });

        _logger.LogInformation("Photo captured from {Device} ({Bytes} bytes).", device.DisplayName, jpeg.Length);
        return $"Foto acquisita da «{device.DisplayName}». {caption} La sto inviando su Telegram o nella chat vocale.";
    }

    public async Task<string> CaptureVideoAsync(
        string deviceQuery,
        int durationSeconds,
        CancellationToken cancellationToken)
    {
        var device = ResolveDevice(deviceQuery);
        if (device is null)
        {
            return DescribeNotFound(deviceQuery);
        }

        if (!device.IsOnline)
        {
            return $"La telecamera «{device.DisplayName}» risulta offline.";
        }

        durationSeconds = Math.Clamp(durationSeconds, 1, _options.CurrentValue.MaxVideoSeconds);
        var clip = await _deviceClient.TryCaptureClipAsync(device.IpAddress, durationSeconds, cancellationToken)
            .ConfigureAwait(false);
        if (clip is null || clip.Length == 0)
        {
            return $"Non sono riuscito a registrare un clip da «{device.DisplayName}».";
        }

        byte[] avi;
        try
        {
            avi = JpegClipCodec.BuildAviFromClip(clip, framesPerSecond: 2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid clip from {Device}.", device.DisplayName);
            return $"Il clip da «{device.DisplayName}» non è valido.";
        }

        var caption =
            $"Video di {durationSeconds}s dalla telecamera «{device.DisplayName}».";
        var fileName = SanitizeFileName(device.DisplayName) + ".avi";

        _mediaBuffer.Enqueue(new CameraTurnMediaItem
        {
            Kind = CameraMediaKind.Video,
            Data = avi,
            FileName = fileName,
            Caption = caption,
            DeviceName = device.DisplayName,
        });

        _logger.LogInformation(
            "Video captured from {Device} ({Seconds}s, {Bytes} bytes AVI).",
            device.DisplayName,
            durationSeconds,
            avi.Length);

        return $"Ho registrato circa {durationSeconds} secondi con «{device.DisplayName}». {caption} Lo sto inviando.";
    }

    public async Task<string> SetMotionDetectionAsync(
        string deviceQuery,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var device = ResolveDevice(deviceQuery);
        if (device is null)
        {
            return DescribeNotFound(deviceQuery);
        }

        _registry.SetMotionDetection(device.Id, enabled);

        if (device.IsOnline && HasUsableIp(device.IpAddress))
        {
            _ = await _deviceClient.TrySetMotionDetectionAsync(device.IpAddress, enabled, cancellationToken)
                .ConfigureAwait(false);
        }

        var state = enabled ? "attivato" : "disattivato";
        return $"Rilevamento movimento {state} per «{device.DisplayName}»."
            + (device.IsOnline ? string.Empty : " (dispositivo offline: la ESP32 applicherà l'impostazione al prossimo heartbeat.)");
    }

    public string StoreMediaForUi(byte[] data, string contentType, string fileName) =>
        _mediaCache.Store(data, contentType, fileName);

    private ConnectedDeviceSnapshot? ResolveDevice(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return _registry.GetSnapshots().FirstOrDefault(d => d.IsOnline && d.Type == "camera")
                ?? _registry.GetSnapshots().FirstOrDefault(d => d.Type == "camera");
        }

        var q = query.Trim();
        var devices = _registry.GetSnapshots();
        var exact = devices.FirstOrDefault(d =>
            string.Equals(d.Id, q, StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.DisplayName, q, StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.HostLabel, q, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var normalized = NormalizeQuery(q);
        return devices
            .OrderByDescending(d => d.IsOnline)
            .FirstOrDefault(d =>
                NormalizeQuery(d.DisplayName).Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || NormalizeQuery(d.HostLabel).Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || NormalizeQuery(d.Id).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeQuery(string value) =>
        value.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

    private static string DescribeNotFound(string query) =>
        string.IsNullOrWhiteSpace(query)
            ? "Nessuna telecamera trovata. Chiedi prima l'elenco dispositivi."
            : $"Nessuna telecamera corrisponde a «{query}». Usa list_connected_devices per i nomi esatti.";

    private async Task<string> BuildPhotoCaptionAsync(
        byte[] jpeg,
        string deviceName,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (!options.VisionAnalysisEnabled)
        {
            return $"Scatto da {deviceName}.";
        }

        var description = await _vision.TryDescribeImageAsync(jpeg, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(description))
        {
            return $"Scatto da {deviceName}.";
        }

        try
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                options.VisionCaptionFormat,
                description);
        }
        catch (FormatException)
        {
            return description;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Trim();
    }

    private static bool HasUsableIp(string ip) =>
        !string.IsNullOrWhiteSpace(ip) && !string.Equals(ip, "—", StringComparison.Ordinal);
}
