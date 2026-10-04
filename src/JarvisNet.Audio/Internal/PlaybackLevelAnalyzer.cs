namespace JarvisNet.Audio.Internal;

internal static class PlaybackLevelAnalyzer
{
    public static float[] ComputeEnvelope(ReadOnlySpan<byte> pcm16Le, int sampleRate, int windowMs = 40)
    {
        if (pcm16Le.Length < 2 || sampleRate <= 0)
        {
            return [];
        }

        var windowSamples = Math.Max(1, sampleRate * windowMs / 1000);
        var sampleCount = pcm16Le.Length / 2;
        var windowCount = (sampleCount + windowSamples - 1) / windowSamples;
        var envelope = new float[windowCount];

        for (var w = 0; w < windowCount; w++)
        {
            var startSample = w * windowSamples;
            var endSample = Math.Min(sampleCount, startSample + windowSamples);
            var count = endSample - startSample;
            if (count <= 0)
            {
                continue;
            }

            double sum = 0;
            for (var i = startSample; i < endSample; i++)
            {
                var index = i * 2;
                var value = BitConverter.ToInt16(pcm16Le.Slice(index, 2)) / 32768.0;
                sum += value * value;
            }

            var rms = (float)Math.Sqrt(sum / count);
            envelope[w] = Math.Clamp(rms * 5f, 0f, 1f);
        }

        return envelope;
    }
}
