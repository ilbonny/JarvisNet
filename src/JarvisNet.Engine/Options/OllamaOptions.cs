namespace JarvisNet.Engine.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "JarvisNet:Ollama";

    public string Endpoint { get; set; } = "http://localhost:11434";

    public string ModelName { get; set; } = "jarvis:1b-new";

    public string SystemPrompt { get; set; } =
        "You are JarvisNet, a concise voice assistant. Reply briefly and naturally for text-to-speech, in the same language as the user.";

    public float Temperature { get; set; } = 0.7f;
}
