using JarvisNet.Audio.Models;

namespace JarvisNet.Audio.Internal;

internal static class AudioPcmConverter
{
    public static float[] BytesToFloatSamples(ReadOnlySpan<byte> buffer, int bytesRecorded)
    {
        var sampleCount = bytesRecorded / 2;
        var samples = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var index = i * 2;
            var value = BitConverter.ToInt16(buffer.Slice(index, 2));
            samples[i] = value / 32768f;
        }

        return samples;
    }

    public static float[] ShortsToFloatSamples(ReadOnlySpan<short> samples)
    {
        var output = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            output[i] = samples[i] / 32768f;
        }

        return output;
    }

    public static float ComputeRms(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
        {
            return 0f;
        }

        double sum = 0;
        foreach (var sample in samples)
        {
            sum += sample * sample;
        }

        return (float)Math.Sqrt(sum / samples.Length);
    }

    public static bool ShouldForwardFrame(
        AudioInputMode mode,
        bool pushToTalkActive,
        float rms,
        float vadThreshold,
        ref bool vadGateOpen)
    {
        return mode switch
        {
            AudioInputMode.PushToTalk => pushToTalkActive,
            AudioInputMode.Vad => UpdateVadGate(rms, vadThreshold, ref vadGateOpen),
            _ => true,
        };
    }

    private static bool UpdateVadGate(float rms, float vadThreshold, ref bool vadGateOpen)
    {
        if (rms >= vadThreshold)
        {
            vadGateOpen = true;
            return true;
        }

        if (vadGateOpen && rms >= vadThreshold * 0.5f)
        {
            return true;
        }

        vadGateOpen = false;
        return false;
    }
}
