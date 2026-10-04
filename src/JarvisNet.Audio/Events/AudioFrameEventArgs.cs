namespace JarvisNet.Audio.Events;

public sealed class AudioFrameEventArgs : EventArgs
{
    public AudioFrameEventArgs(float[] samples, int sampleRate)
    {
        Samples = samples;
        SampleRate = sampleRate;
    }

    public float[] Samples { get; }

    public int SampleRate { get; }
}
