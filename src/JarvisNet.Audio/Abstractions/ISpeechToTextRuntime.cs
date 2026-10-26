namespace JarvisNet.Audio.Abstractions;

public interface ISpeechToTextRuntime
{
    Task ReloadAfterProfileChangeAsync(CancellationToken cancellationToken = default);
}
