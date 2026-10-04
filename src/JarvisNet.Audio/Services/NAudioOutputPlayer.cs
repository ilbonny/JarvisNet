using NAudio.Wave;

namespace JarvisNet.Audio.Services;

internal sealed class NAudioOutputPlayer
{
    public Task PlayPcmAsync(IWavePlayer wavePlayer, byte[] pcm, WaveFormat waveFormat, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            using var memoryStream = new MemoryStream(pcm);
            using var waveStream = new RawSourceWaveStream(memoryStream, waveFormat);
            wavePlayer.Init(waveStream);
            wavePlayer.Play();

            while (wavePlayer.PlaybackState == PlaybackState.Playing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(50);
            }
        }, cancellationToken);
    }
}
