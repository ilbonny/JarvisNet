using JarvisNet.Engine.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JarvisNet.Engine.Services;

public sealed class OllamaBrainService
{
    private readonly IChatCompletionService _chatCompletion;
    private readonly Kernel _kernel;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaBrainService> _logger;
    private readonly IMcpTurnScope? _mcpTurnScope;
    private readonly ChatHistory _history;
    private readonly object _historyLock = new();

    public OllamaBrainService(
        IChatCompletionService chatCompletion,
        Kernel kernel,
        IOptions<OllamaOptions> options,
        IMcpTurnScope mcpTurnScope,
        ILogger<OllamaBrainService> logger)
    {
        _chatCompletion = chatCompletion;
        _kernel = kernel;
        _options = options.Value;
        _mcpTurnScope = mcpTurnScope;
        _logger = logger;
        _history = CreateInitialHistory(_options.SystemPrompt);
    }

    internal OllamaBrainService(
        IChatCompletionService chatCompletion,
        Kernel kernel,
        OllamaOptions options,
        ILogger<OllamaBrainService> logger,
        IMcpTurnScope? mcpTurnScope = null)
        : this(
            chatCompletion,
            kernel,
            Microsoft.Extensions.Options.Options.Create(options),
            mcpTurnScope ?? NullMcpTurnScope.Instance,
            logger)
    {
    }

    public async Task<string> SendAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return string.Empty;
        }

        ChatHistory historySnapshot;
        lock (_historyLock)
        {
            _history.AddUserMessage(userMessage.Trim());
            historySnapshot = CloneHistory(_history);
        }

        _logger.LogDebug("Invio messaggio utente a Ollama ({Model}).", _options.ModelName);

        var assistantText = await CompleteAssistantTurnAsync(
                userMessage.Trim(),
                historySnapshot,
                cancellationToken)
            .ConfigureAwait(false);

        lock (_historyLock)
        {
            if (!string.IsNullOrEmpty(assistantText))
            {
                _history.AddAssistantMessage(assistantText);
            }
        }

        return assistantText;
    }

    public async Task<string> SendStreamingAsync(
        string userMessage,
        Action<string> onChunk,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return string.Empty;
        }

        ChatHistory historySnapshot;
        lock (_historyLock)
        {
            _history.AddUserMessage(userMessage.Trim());
            historySnapshot = CloneHistory(_history);
        }

        _logger.LogDebug(
            "Invio messaggio utente (voce) a Ollama ({Model}) con auto-invoke tool (non streaming SK/Ollama).",
            _options.ModelName);

        // Ollama + GetStreamingChatMessageContentsAsync non esegue i tool MCP: il modello
        // tende a stampare pseudo-codice nel testo. GetChatMessageContentAsync con kernel
        // attiva l'auto-invoke di Semantic Kernel (FunctionChoiceBehavior.Auto).
        var assistantText = await CompleteAssistantTurnAsync(
                userMessage.Trim(),
                historySnapshot,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var chunk in ChunkTextForUi(assistantText))
        {
            onChunk(chunk);
        }

        lock (_historyLock)
        {
            if (!string.IsNullOrEmpty(assistantText))
            {
                _history.AddAssistantMessage(assistantText);
            }
        }

        return assistantText;
    }

    public void ClearHistory()
    {
        lock (_historyLock)
        {
            _history.Clear();
            _history.AddSystemMessage(_options.SystemPrompt);
        }
    }

    public IReadOnlyList<ChatMessageContent> GetHistorySnapshot()
    {
        lock (_historyLock)
        {
            return _history
                .Select(message => new ChatMessageContent(message.Role, message.Content ?? string.Empty))
                .ToList();
        }
    }

    private async Task<string> CompleteAssistantTurnAsync(
        string userMessage,
        ChatHistory historySnapshot,
        CancellationToken cancellationToken)
    {
        _mcpTurnScope?.BeginUserTurn();

        var executionSettings = CreateExecutionSettings();
        var response = await _chatCompletion
            .GetChatMessageContentAsync(historySnapshot, executionSettings, kernel: _kernel, cancellationToken)
            .ConfigureAwait(false);

        var assistantText = response.Content?.Trim() ?? string.Empty;
        if (!BrowserResponseGuard.ShouldRetryWithoutTools(
                userMessage,
                assistantText,
                _mcpTurnScope?.ToolsInvokedThisTurn ?? 0))
        {
            return assistantText;
        }

        _logger.LogWarning(
            "Risposta web sospetta senza tool MCP (turno utente: {Preview}); ritento con nudge.",
            userMessage.Length > 80 ? userMessage[..80] + "…" : userMessage);

        _mcpTurnScope?.BeginUserTurn();
        var retryHistory = CloneHistory(historySnapshot);
        retryHistory.AddUserMessage(BrowserResponseGuard.ToolNudgeUserMessage);

        response = await _chatCompletion
            .GetChatMessageContentAsync(retryHistory, executionSettings, kernel: _kernel, cancellationToken)
            .ConfigureAwait(false);

        return response.Content?.Trim() ?? string.Empty;
    }

    private OpenAIPromptExecutionSettings CreateExecutionSettings() =>
        new()
        {
            Temperature = _options.Temperature,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };

    private sealed class NullMcpTurnScope : IMcpTurnScope
    {
        public static readonly NullMcpTurnScope Instance = new();

        public int ToolsInvokedThisTurn => 0;

        public void BeginUserTurn()
        {
        }
    }

    private static ChatHistory CreateInitialHistory(string systemPrompt)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);
        return history;
    }

    private static ChatHistory CloneHistory(ChatHistory source)
    {
        var clone = new ChatHistory();
        foreach (var message in source)
        {
            clone.Add(new ChatMessageContent(message.Role, message.Content ?? string.Empty));
        }

        return clone;
    }

    private static IEnumerable<string> ChunkTextForUi(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        const int chunkSize = 16;
        for (var i = 0; i < text.Length; i += chunkSize)
        {
            var length = Math.Min(chunkSize, text.Length - i);
            yield return text.Substring(i, length);
        }
    }
}
