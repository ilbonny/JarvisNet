namespace JarvisNet.Engine.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "JarvisNet:Ollama";

    public string Endpoint { get; set; } = "http://localhost:11434";

    public string ModelName { get; set; } = "jarvis:1b-new";

    public string SystemPrompt { get; set; } =
        "Sei JarvisNet, un assistente vocale italiano efficiente, conciso e diretto. Rispondi sempre in modo breve e naturale per la sintesi vocale.";

    public float Temperature { get; set; } = 0.7f;
}
