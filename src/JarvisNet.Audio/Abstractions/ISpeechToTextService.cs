using JarvisNet.Audio.Events;

namespace JarvisNet.Audio.Abstractions;

public interface ISpeechToTextService : IAsyncDisposable
{
    event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;

    Task StartSessionAsync(CancellationToken cancellationToken = default);

    Task StopSessionAsync(CancellationToken cancellationToken = default);

    void ProcessAudio(ReadOnlyMemory<float> samples, int sampleRate);
}
