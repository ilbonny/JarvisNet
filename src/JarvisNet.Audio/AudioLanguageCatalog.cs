using JarvisNet.Audio.Models;

namespace JarvisNet.Audio;

public static class AudioLanguageCatalog
{
    public sealed record LanguageProfile(
        string Id,
        string Label,
        string CultureName,
        string SttFolder,
        string DefaultVoiceKey,
        string TtsEchoTemplate);

    private static readonly LanguageProfile[] Profiles =
    [
        new(
            "it_IT",
            "Italiano",
            "it-IT",
            "it-kroko",
            PiperVoiceCatalog.PaolaMedium,
            "Ho sentito la tua voce. La trascrizione è: {0}"),
        new(
            "es_ES",
            "Español (España)",
            "es-ES",
            "es-kroko",
            PiperVoiceCatalog.EsSharvardMedium,
            "He escuchado tu voz. La transcripción es: {0}"),
        new(
            "es_AR",
            "Español (Argentina)",
            "es-AR",
            "es-kroko",
            PiperVoiceCatalog.EsDanielaHigh,
            "He escuchado tu voz. La transcripción es: {0}"),
        new(
            "en_US",
            "English (US)",
            "en-US",
            "en-kroko",
            PiperVoiceCatalog.EnLjspeechHigh,
            "I heard your voice. The transcription is: {0}"),
    ];

    public static IReadOnlyList<LanguageProfile> All => Profiles;

    public static bool TryGet(string languageId, out LanguageProfile profile)
    {
        foreach (var candidate in Profiles)
        {
            if (string.Equals(candidate.Id, languageId, StringComparison.OrdinalIgnoreCase))
            {
                profile = candidate;
                return true;
            }
        }

        profile = null!;
        return false;
    }

    public static string ResolveLanguageId(AudioOptions options, string cultureName)
    {
        foreach (var profile in Profiles)
        {
            if (options.SherpaEncoderPath.Contains(profile.SttFolder, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(profile.CultureName, cultureName, StringComparison.OrdinalIgnoreCase))
                {
                    return profile.Id;
                }

                if (profile.SttFolder == "en-kroko")
                {
                    return "en_US";
                }

                if (profile.SttFolder == "es-kroko" &&
                    cultureName.StartsWith("es-", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Profiles.FirstOrDefault(p =>
                        string.Equals(p.CultureName, cultureName, StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        return match.Id;
                    }

                    return PiperVoiceCatalog.ResolveVoiceKey(options) switch
                    {
                        var key when key.StartsWith("es_AR", StringComparison.OrdinalIgnoreCase) => "es_AR",
                        _ => "es_ES",
                    };
                }

                return profile.Id;
            }
        }

        return "it_IT";
    }

    public static bool IsSttInstalled(LanguageProfile profile, AudioOptions options)
    {
        var encoder = options.ResolvePath($"stt/{profile.SttFolder}/encoder.onnx");
        var decoder = options.ResolvePath($"stt/{profile.SttFolder}/decoder.onnx");
        var joiner = options.ResolvePath($"stt/{profile.SttFolder}/joiner.onnx");
        var tokens = options.ResolvePath($"stt/{profile.SttFolder}/tokens.txt");
        return File.Exists(encoder) && File.Exists(decoder) && File.Exists(joiner) && File.Exists(tokens);
    }

    public static void ApplyToOptions(LanguageProfile profile, AudioOptions options)
    {
        options.SherpaEncoderPath = $"stt/{profile.SttFolder}/encoder.onnx";
        options.SherpaDecoderPath = $"stt/{profile.SttFolder}/decoder.onnx";
        options.SherpaJoinerPath = $"stt/{profile.SttFolder}/joiner.onnx";
        options.SherpaTokensPath = $"stt/{profile.SttFolder}/tokens.txt";
        options.SherpaModelType = "zipformer2";
        options.TtsEchoTemplate = profile.TtsEchoTemplate;
    }
}
