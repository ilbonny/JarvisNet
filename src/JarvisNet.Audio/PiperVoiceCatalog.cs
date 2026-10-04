namespace JarvisNet.Audio;

/// <summary>
/// Voci Piper italiane (it_IT) note, relative a <c>models/</c>.
/// </summary>
public static class PiperVoiceCatalog
{
    public const string PaolaMedium = "it_IT-paola-medium";
    public const string RiccardoXLow = "it_IT-riccardo-x_low";
    public const string SerenaMedium = "it_IT-serena-medium";

    private static readonly IReadOnlyDictionary<string, PiperVoiceInfo> Voices =
        new Dictionary<string, PiperVoiceInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [PaolaMedium] = new("tts/it_IT-paola-medium.onnx", "Paola", "medium", Gender: "female"),
            [RiccardoXLow] = new("tts/it_IT-riccardo-x_low.onnx", "Riccardo", "x_low", Gender: "male"),
            [SerenaMedium] = new("tts/it_IT-serena-medium.onnx", "Serena", "medium", Gender: "female"),
        };

    public static IReadOnlyDictionary<string, PiperVoiceInfo> ItalianVoices => Voices;

    public static bool TryGetModelRelativePath(string voiceKey, out string relativePath)
    {
        if (Voices.TryGetValue(voiceKey, out var info))
        {
            relativePath = info.ModelRelativePath;
            return true;
        }

        relativePath = string.Empty;
        return false;
    }

    public static IEnumerable<string> ListVoiceKeys() => Voices.Keys;
}

public sealed record PiperVoiceInfo(
    string ModelRelativePath,
    string SpeakerName,
    string Quality,
    string Gender);
