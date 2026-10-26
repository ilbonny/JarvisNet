using JarvisNet.Audio.Models;

namespace JarvisNet.Audio.Abstractions;

public interface IPiperVoiceService
{
    string GetCurrentVoiceKey();

    IReadOnlyList<PiperVoiceOption> ListVoices();

    Task SetVoiceAsync(string voiceKey, CancellationToken cancellationToken = default);
}
