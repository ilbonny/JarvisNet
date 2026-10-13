using System.Text.Json;
using PiperSharp.Models;

namespace JarvisNet.Audio.Internal;

internal static class PiperVoiceModelLoader
{
    public static async Task<VoiceModel> LoadFromOnnxPathAsync(string onnxPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(onnxPath))
        {
            throw new FileNotFoundException($"Piper voice model not found at '{onnxPath}'.", onnxPath);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(onnxPath))
            ?? throw new InvalidOperationException($"Could not resolve directory for '{onnxPath}'.");
        var fileName = Path.GetFileName(onnxPath);
        var configPath = onnxPath + ".json";
        VoiceAudio? audio = null;
        if (File.Exists(configPath))
        {
            await using var stream = File.OpenRead(configPath);
            audio = await ReadVoiceAudioAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        return new VoiceModel
        {
            Key = Path.GetFileNameWithoutExtension(fileName),
            ModelLocation = directory,
            Files = new Dictionary<string, dynamic> { [fileName] = fileName },
            Audio = audio,
        };
    }

    private static async Task<VoiceAudio?> ReadVoiceAudioAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("audio", out var audioElement))
        {
            return null;
        }

        uint sampleRate = 22050;
        if (audioElement.TryGetProperty("sample_rate", out var sampleRateElement) &&
            sampleRateElement.TryGetUInt32(out var parsedSampleRate))
        {
            sampleRate = parsedSampleRate;
        }

        var qualityToken = audioElement.TryGetProperty("quality", out var qualityElement)
            ? qualityElement.GetString()
            : null;

        return new VoiceAudio
        {
            SampleRate = sampleRate,
            Quality = MapQuality(qualityToken),
        };
    }

    internal static VoiceQuality MapQuality(string? qualityToken)
    {
        if (string.IsNullOrWhiteSpace(qualityToken))
        {
            return VoiceQuality.Medium;
        }

        var normalized = qualityToken.Trim().Replace('-', '_');
        if (Enum.TryParse<VoiceQuality>(normalized, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        // OpenVoiceOS / export variants (e.g. "training") — not in PiperSharp enum.
        return VoiceQuality.Medium;
    }
}
