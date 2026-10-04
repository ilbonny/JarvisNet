namespace JarvisNet.Audio.Abstractions;

public interface ITextToSpeechService
{
    Task<byte[]> SynthesizePcmAsync(string text, CancellationToken cancellationToken = default);

    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
}
