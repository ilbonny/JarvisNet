using System.Text.Json;
using System.Text.Json.Serialization;
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
            var config = await JsonSerializer.DeserializeAsync<PiperOnnxConfig>(
                    stream,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            audio = config?.Audio;
        }

        return new VoiceModel
        {
            Key = Path.GetFileNameWithoutExtension(fileName),
            ModelLocation = directory,
            Files = new Dictionary<string, dynamic> { [fileName] = fileName },
            Audio = audio,
        };
    }

    private sealed class PiperOnnxConfig
    {
        [JsonPropertyName("audio")]
        public VoiceAudio? Audio { get; set; }
    }
}
