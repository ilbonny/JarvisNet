using JarvisNet.Audio.Events;

namespace JarvisNet.Audio.Abstractions;

public interface ITextToSpeechService
{
    event EventHandler? SpeakingStarted;

    event EventHandler? SpeakingCompleted;

    /// <summary>
    /// RMS level of TTS playback (0–1), emitted in sync with spoken audio.
    /// </summary>
    event EventHandler<AudioLevelChangedEventArgs>? PlaybackLevelChanged;

    Task<byte[]> SynthesizePcmAsync(string text, CancellationToken cancellationToken = default);

    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
}
