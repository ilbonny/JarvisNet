using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JarvisNet.Plugins.Sdk.Abstractions;

/// <summary>
/// Estensione opzionale del turno conversazionale fornita da un plugin Jarvis (dopo la prima risposta LLM).
/// </summary>
public interface IJarvisPluginTurnHandler
{
    /// <summary>Priorità rispetto ad altri handler (minore = prima).</summary>
    int Order => 0;

    Task<JarvisPluginTurnOutcome?> TryEnhanceTurnAsync(
        JarvisPluginTurnContext context,
        CancellationToken cancellationToken);
}

public sealed class JarvisPluginTurnContext
{
    public required string UserMessage { get; init; }

    public required string AssistantText { get; set; }

    public required IJarvisTurnScope TurnScope { get; init; }

    public required ChatHistory HistorySnapshot { get; init; }

    public required Kernel Kernel { get; init; }

    public required IChatCompletionService ChatCompletion { get; init; }

    public required OpenAIPromptExecutionSettings ExecutionSettings { get; init; }

    public required double Temperature { get; init; }

    public required Func<ChatHistory, CancellationToken, Task<string>> InvokeAssistantAsync { get; init; }

    public required ILogger Logger { get; init; }
}

public sealed record JarvisPluginTurnOutcome(string? Response)
{
    public bool IsHandled => Response is not null;
}
