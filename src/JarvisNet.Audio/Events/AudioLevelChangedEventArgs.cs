namespace JarvisNet.Audio.Events;

public sealed class AudioLevelChangedEventArgs : EventArgs
{
    public AudioLevelChangedEventArgs(float level, DateTimeOffset timestamp)
    {
        Level = level;
        Timestamp = timestamp;
    }

    public float Level { get; }

    public DateTimeOffset Timestamp { get; }
}
