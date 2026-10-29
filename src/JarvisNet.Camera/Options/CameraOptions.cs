namespace JarvisNet.Camera.Options;

public sealed class CameraOptions
{
    public const string SectionName = "JarvisNet:Camera";

    public bool Enabled { get; set; }

    /// <summary>When set, POST /api/camera/motion must send header X-Api-Key with this value.</summary>
    public string IngestApiKey { get; set; } = string.Empty;

    public string ResolvedIngestApiKey => IngestApiKey.Trim();

    /// <summary>Telegram chat ids to notify. When empty, uses JarvisNet:Telegram:AllowedChatIds.</summary>
    public long[] NotifyChatIds { get; set; } = [];

    public int MinSecondsBetweenAlerts { get; set; } = 30;

    /// <summary>Devices without heartbeat/motion for this many seconds show as offline in the UI.</summary>
    public int DeviceOnlineSeconds { get; set; } = 120;

    /// <summary>HTTP port for on-device control (capture / clip / motion).</summary>
    public int DeviceControlPort { get; set; } = 80;

    /// <summary>Max seconds for capture_video (ESP32 clip).</summary>
    public int MaxVideoSeconds { get; set; } = 10;

    /// <summary>Default motion detection for newly seen cameras (usually false).</summary>
    public bool DefaultMotionDetectionEnabled { get; set; }

    public string MotionCaption { get; set; } = "Movimento rilevato dalla ESP32-CAM";

    public bool VisionAnalysisEnabled { get; set; } = true;

    /// <summary>Small Ollama vision-only model (default moondream ~1.7GB). Do not reuse Jarvis chat models like qwen3.5:9b.</summary>
    public string VisionModelName { get; set; } = "moondream";

    /// <summary>Ollama keep_alive in seconds; 0 unloads vision model from GPU right after each caption.</summary>
    public int VisionKeepAliveSeconds { get; set; }

    /// <summary>Max tokens for the one-line caption.</summary>
    public int VisionMaxTokens { get; set; } = 48;

    public string VisionPrompt { get; set; } =
        "Descrivi in una sola breve frase in italiano cosa vedi in questa foto di sorveglianza. " +
        "Esempi: una persona con maglietta nera; un gatto sul divano; nessuno in vista. " +
        "Solo la frase, massimo 15 parole, senza virgolette.";

    /// <summary>{0} = vision description. Used when analysis succeeds.</summary>
    public string VisionCaptionFormat { get; set; } = "Da questa foto ho rilevato \"{0}\"";

    public bool IsIngestProtected =>
        !string.IsNullOrWhiteSpace(IngestApiKey);
}
