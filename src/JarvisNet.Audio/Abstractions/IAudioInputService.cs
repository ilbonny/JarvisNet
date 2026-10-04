using JarvisNet.Audio.Events;
using JarvisNet.Audio.Models;

namespace JarvisNet.Audio.Abstractions;

public interface IAudioInputService : IAsyncDisposable
{
    AudioInputMode InputMode { get; }

    event EventHandler<AudioFrameEventArgs>? AudioFrameAvailable;

    event EventHandler<AudioLevelChangedEventArgs>? AudioLevelChanged;

    Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(CancellationToken cancellationToken = default);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    void SetPushToTalkActive(bool active);

    /// <summary>
    /// When true, capture continues but frames are not forwarded (e.g. during TTS playback to avoid echo).
    /// </summary>
    void SetSpeechToTextSuppressed(bool suppressed);
}
