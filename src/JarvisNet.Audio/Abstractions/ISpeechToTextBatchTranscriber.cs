namespace JarvisNet.Audio.Abstractions;

public interface ISpeechToTextBatchTranscriber
{
    Task<string?> TranscribeMonoPcmAsync(
        ReadOnlyMemory<float> samples,
        int sampleRate,
        CancellationToken cancellationToken = default);

    Task<string?> TranscribeOggOpusAsync(Stream oggOpusStream, CancellationToken cancellationToken = default);
}
