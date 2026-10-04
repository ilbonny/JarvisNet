namespace JarvisNet.Audio.Events;

public sealed class SpeechRecognizedEventArgs : EventArgs
{
    public SpeechRecognizedEventArgs(string text, bool isFinal, DateTimeOffset timestamp)
    {
        Text = text;
        IsFinal = isFinal;
        Timestamp = timestamp;
    }

    public string Text { get; }

    public bool IsFinal { get; }

    public DateTimeOffset Timestamp { get; }
}
