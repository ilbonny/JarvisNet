using JarvisNet.Audio.Internal;
using NAudio.Wave;

namespace JarvisNet.Audio.Services;

internal sealed class NAudioOutputPlayer
{
    public Task PlayPcmAsync(
        IWavePlayer wavePlayer,
        byte[] pcm,
        WaveFormat waveFormat,
        Action<float>? onPlaybackLevel,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var envelope = onPlaybackLevel is not null
                ? PlaybackLevelAnalyzer.ComputeEnvelope(pcm, waveFormat.SampleRate)
                : [];

            using var memoryStream = new MemoryStream(pcm);
            using var waveStream = new RawSourceWaveStream(memoryStream, waveFormat);
            wavePlayer.Init(waveStream);
            wavePlayer.Play();

            while (wavePlayer.PlaybackState == PlaybackState.Playing)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (onPlaybackLevel is not null && envelope.Length > 0)
                {
                    var bytesPerSecond = waveFormat.AverageBytesPerSecond;
                    var positionMs = bytesPerSecond > 0
                        ? (int)(waveStream.Position * 1000.0 / bytesPerSecond)
                        : 0;
                    var index = Math.Clamp(positionMs / 40, 0, envelope.Length - 1);
                    onPlaybackLevel(envelope[index]);
                }

                Thread.Sleep(16);
            }

            onPlaybackLevel?.Invoke(0f);
        }, cancellationToken);
    }
}
