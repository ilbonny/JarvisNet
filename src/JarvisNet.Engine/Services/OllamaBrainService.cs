using System.Collections.Concurrent;
using JarvisNet.Core;
using JarvisNet.Engine.Options;
using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.Infrastructure;
using JarvisNet.Plugins.Sdk.Options;
using JarvisNet.Plugins.Sdk.Services;
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
    private readonly IOptionsMonitor<OllamaOptions> _optionsMonitor;
    private readonly IOptionsMonitor<JarvisLocaleOptions> _localeMonitor;
    private readonly ILogger<OllamaBrainService> _logger;
    private readonly IJarvisTurnScope _turnScope;
    private readonly IReadOnlyList<IJarvisPluginTurnHandler> _pluginTurnHandlers;
    private readonly ConcurrentDictionary<string, ChatHistory> _histories = new(StringComparer.Ordinal);
    private readonly object _historyLock = new();
    private readonly SemaphoreSlim _completionLock = new(1, 1);

    public OllamaBrainService(
        IChatCompletionService chatCompletion,
        Kernel kernel,
        IOptionsMonitor<OllamaOptions> options,
        IOptionsMonitor<JarvisLocaleOptions> locale,
        IJarvisTurnScope turnScope,
        ILogger<OllamaBrainService> logger,
        IEnumerable<IJarvisPluginTurnHandler>? pluginTurnHandlers = null)
    {
        _chatCompletion = chatCompletion;
        _kernel = kernel;
        _optionsMonitor = options;
        _localeMonitor = locale;
        _turnScope = turnScope;
        _logger = logger;
        _pluginTurnHandlers = pluginTurnHandlers?.ToList() ?? [];
        _ = GetOrCreateHistory(JarvisChatSessionKeys.Voice);
    }

    internal OllamaBrainService(
        IChatCompletionService chatCompletion,
        Kernel kernel,
        OllamaOptions options,
        ILogger<OllamaBrainService> logger,
        IJarvisTurnScope? turnScope = null,
        IEnumerable<IJarvisPluginTurnHandler>? pluginTurnHandlers = null)
        : this(
            chatCompletion,
            kernel,
            new TestOptionsMonitor<OllamaOptions>(options),
            new TestOptionsMonitor<JarvisLocaleOptions>(new JarvisLocaleOptions()),
            turnScope ?? NullJarvisTurnScope.Instance,
            logger,
            pluginTurnHandlers)
    {
    }

    public Task<string> SendAsync(
        string userMessage,
        CancellationToken cancellationToken = default) =>
        SendAsync(userMessage, JarvisChatSessionKeys.Voice, cancellationToken);

    public async Task<string> SendAsync(
        string userMessage,
        string sessionKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return string.Empty;
        }

        var trimmed = userMessage.Trim();
        ChatHistory historySnapshot;
        lock (_historyLock)
        {
            var history = GetOrCreateHistory(sessionKey);
            history.AddUserMessage(trimmed);
            historySnapshot = CloneHistory(history);
        }

        _logger.LogDebug(
            "Invio messaggio utente a Ollama ({Model}), sessione {SessionKey}.",
            _optionsMonitor.CurrentValue.ModelName,
            sessionKey);

        await _completionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var assistantText = await CompleteAssistantTurnAsync(trimmed, historySnapshot, sessionKey, cancellationToken)
                .ConfigureAwait(false);

            lock (_historyLock)
            {
                if (!string.IsNullOrEmpty(assistantText))
                {
                    GetOrCreateHistory(sessionKey).AddAssistantMessage(assistantText);
                }
            }

            return assistantText;
        }
        finally
        {
            _completionLock.Release();
        }
    }

    public Task<string> SendStreamingAsync(
        string userMessage,
        Action<string> onChunk,
        CancellationToken cancellationToken = default) =>
        SendStreamingAsync(userMessage, JarvisChatSessionKeys.Voice, onChunk, cancellationToken);

    public async Task<string> SendStreamingAsync(
        string userMessage,
        string sessionKey,
        Action<string> onChunk,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return string.Empty;
        }

        var trimmed = userMessage.Trim();
        ChatHistory historySnapshot;
        lock (_historyLock)
        {
            var history = GetOrCreateHistory(sessionKey);
            history.AddUserMessage(trimmed);
            historySnapshot = CloneHistory(history);
        }

        _logger.LogDebug(
            "Invio messaggio utente (voce) a Ollama ({Model}) con auto-invoke tool, sessione {SessionKey}.",
            _optionsMonitor.CurrentValue.ModelName,
            sessionKey);

        await _completionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var assistantText = await CompleteAssistantTurnAsync(trimmed, historySnapshot, sessionKey, cancellationToken)
                .ConfigureAwait(false);

            foreach (var chunk in ChunkTextForUi(assistantText))
            {
                onChunk(chunk);
            }

            lock (_historyLock)
            {
                if (!string.IsNullOrEmpty(assistantText))
                {
                    GetOrCreateHistory(sessionKey).AddAssistantMessage(assistantText);
                }
            }

            return assistantText;
        }
        finally
        {
            _completionLock.Release();
        }
    }

    public void ClearHistory() => ClearHistory(JarvisChatSessionKeys.Voice);

    public void ClearHistory(string sessionKey)
    {
        lock (_historyLock)
        {
            _histories[sessionKey] = CreateInitialHistory(BuildSystemPrompt());
        }
    }

    public IReadOnlyList<ChatMessageContent> GetHistorySnapshot() =>
        GetHistorySnapshot(JarvisChatSessionKeys.Voice);

    public IReadOnlyList<ChatMessageContent> GetHistorySnapshot(string sessionKey)
    {
        lock (_historyLock)
        {
            return GetOrCreateHistory(sessionKey)
                .Select(message => new ChatMessageContent(message.Role, message.Content ?? string.Empty))
                .ToList();
        }
    }

    private ChatHistory GetOrCreateHistory(string sessionKey) =>
        _histories.GetOrAdd(sessionKey, _ => CreateInitialHistory(BuildSystemPrompt()));

    private async Task<string> CompleteAssistantTurnAsync(
        string userMessage,
        ChatHistory historySnapshot,
        string sessionKey,
        CancellationToken cancellationToken)
    {
        _turnScope.BeginUserTurn(sessionKey);

        var executionSettings = CreateExecutionSettings();
        var response = await _chatCompletion
            .GetChatMessageContentAsync(historySnapshot, executionSettings, kernel: _kernel, cancellationToken)
            .ConfigureAwait(false);

        var assistantText = response.Content?.Trim() ?? string.Empty;

        if (_pluginTurnHandlers.Count == 0)
        {
            return assistantText;
        }

        var turnContext = new JarvisPluginTurnContext
        {
            UserMessage = userMessage,
            AssistantText = assistantText,
            TurnScope = _turnScope,
            HistorySnapshot = historySnapshot,
            Kernel = _kernel,
            ChatCompletion = _chatCompletion,
            ExecutionSettings = executionSettings,
            Temperature = _optionsMonitor.CurrentValue.Temperature,
            Logger = _logger,
            InvokeAssistantAsync = (history, ct) => InvokeAssistantAsync(history, executionSettings, ct),
        };

        var enhanced = await JarvisPluginTurnCoordinator
            .TryEnhanceTurnAsync(_pluginTurnHandlers, turnContext, cancellationToken)
            .ConfigureAwait(false);

        return enhanced ?? assistantText;
    }

    private async Task<string> InvokeAssistantAsync(
        ChatHistory history,
        OpenAIPromptExecutionSettings executionSettings,
        CancellationToken cancellationToken)
    {
        var response = await _chatCompletion
            .GetChatMessageContentAsync(history, executionSettings, kernel: _kernel, cancellationToken)
            .ConfigureAwait(false);

        return response.Content?.Trim() ?? string.Empty;
    }

    private OpenAIPromptExecutionSettings CreateExecutionSettings() =>
        new()
        {
            Temperature = _optionsMonitor.CurrentValue.Temperature,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };

    private string BuildSystemPrompt() =>
        JarvisSystemPromptComposer.Compose(
            _optionsMonitor.CurrentValue.SystemPrompt,
            _localeMonitor.CurrentValue.CultureName);

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class NullJarvisTurnScope : IJarvisTurnScope
    {
        public static readonly NullJarvisTurnScope Instance = new();

        public int ToolsInvokedThisTurn => 0;

        public string CurrentSessionKey => JarvisChatSessionKeys.Voice;

        public void BeginUserTurn(string sessionKey)
        {
        }

        public void ResetToolsInvokedThisTurn()
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
