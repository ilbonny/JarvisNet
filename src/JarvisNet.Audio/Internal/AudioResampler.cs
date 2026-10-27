namespace JarvisNet.Audio.Internal;

internal static class AudioResampler
{
    public static float[] ResampleToRate(ReadOnlySpan<float> samples, int sourceRate, int targetRate)
    {
        if (samples.Length == 0 || sourceRate <= 0 || targetRate <= 0)
        {
            return [];
        }

        if (sourceRate == targetRate)
        {
            return samples.ToArray();
        }

        if (sourceRate % targetRate == 0)
        {
            var factor = sourceRate / targetRate;
            var outputLength = samples.Length / factor;
            var output = new float[outputLength];
            for (var i = 0; i < outputLength; i++)
            {
                output[i] = samples[i * factor];
            }

            return output;
        }

        var ratio = (double)sourceRate / targetRate;
        var estimatedLength = (int)Math.Ceiling(samples.Length / ratio);
        var resampled = new float[estimatedLength];
        for (var i = 0; i < estimatedLength; i++)
        {
            var sourceIndex = i * ratio;
            var index0 = (int)Math.Floor(sourceIndex);
            var index1 = Math.Min(index0 + 1, samples.Length - 1);
            var fraction = sourceIndex - index0;
            resampled[i] = (float)((1 - fraction) * samples[index0] + fraction * samples[index1]);
        }

        return resampled;
    }
}
