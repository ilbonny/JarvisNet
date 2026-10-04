namespace JarvisNet.Audio;

/// <summary>
/// Voci Piper note, relative a <c>models/</c>.
/// </summary>
public static class PiperVoiceCatalog
{
    public const string PaolaMedium = "it_IT-paola-medium";
    public const string RiccardoXLow = "it_IT-riccardo-x_low";
    public const string SerenaMedium = "it_IT-serena-medium";
    public const string EsSharvardMedium = "es_ES-sharvard-medium";
    public const string EsMls10246Low = "es_ES-mls_10246-low";
    public const string EsDavefxMedium = "es_ES-davefx-medium";
    public const string EsCarlfmXLow = "es_ES-carlfm-x_low";
    public const string EsDanielaHigh = "es_AR-daniela-high";

    private static readonly IReadOnlyDictionary<string, PiperVoiceInfo> Voices =
        new Dictionary<string, PiperVoiceInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [PaolaMedium] = new("tts/it_IT-paola-medium.onnx", "Paola", "medium", "female", "it_IT"),
            [RiccardoXLow] = new("tts/it_IT-riccardo-x_low.onnx", "Riccardo", "x_low", "male", "it_IT"),
            [SerenaMedium] = new("tts/it_IT-serena-medium.onnx", "Serena", "medium", "female", "it_IT"),
            [EsSharvardMedium] = new("tts/es_ES-sharvard-medium.onnx", "Sharvard", "medium", "female", "es_ES", DefaultSpeakerId: 1),
            [EsMls10246Low] = new("tts/es_ES-mls_10246-low.onnx", "MLS 10246", "low", "female", "es_ES"),
            [EsDavefxMedium] = new("tts/es_ES-davefx-medium.onnx", "Davefx", "medium", "neutral", "es_ES"),
            [EsCarlfmXLow] = new("tts/es_ES-carlfm-x_low.onnx", "Carlfm", "x_low", "male", "es_ES"),
            [EsDanielaHigh] = new("tts/es_AR-daniela-high.onnx", "Daniela", "high", "female", "es_AR"),
        };

    public static IReadOnlyDictionary<string, PiperVoiceInfo> AllVoices => Voices;

    public static IReadOnlyDictionary<string, PiperVoiceInfo> ItalianVoices =>
        Voices.Where(v => v.Value.Locale.StartsWith("it_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, PiperVoiceInfo> SpanishVoices =>
        Voices.Where(v => v.Value.Locale.StartsWith("es_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    public static bool TryGetModelRelativePath(string voiceKey, out string relativePath)
    {
        if (TryGetVoice(voiceKey, out var info))
        {
            relativePath = info.ModelRelativePath;
            return true;
        }

        relativePath = string.Empty;
        return false;
    }

    public static bool TryGetVoice(string voiceKey, out PiperVoiceInfo voice) =>
        Voices.TryGetValue(voiceKey, out voice!);

    public static IEnumerable<string> ListVoiceKeys() => Voices.Keys;
}

public sealed record PiperVoiceInfo(
    string ModelRelativePath,
    string SpeakerName,
    string Quality,
    string Gender,
    string Locale,
    int DefaultSpeakerId = 0);
