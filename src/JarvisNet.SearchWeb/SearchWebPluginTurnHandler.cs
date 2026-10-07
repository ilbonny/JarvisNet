using System.Globalization;
using JarvisNet.Plugins.Sdk;
using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.Infrastructure;
using JarvisNet.Plugins.Sdk.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JarvisNet.SearchWeb;

public sealed class SearchWebPluginTurnHandler : IJarvisPluginTurnHandler
{
    private readonly ILogger<SearchWebPluginTurnHandler> _logger;
    private readonly CultureInfo _responseCulture;

    public SearchWebPluginTurnHandler(
        ILogger<SearchWebPluginTurnHandler> logger,
        IOptions<JarvisLocaleOptions> localeOptions)
    {
        _logger = logger;
        _responseCulture = JarvisLocaleResolver.CreateCulture(localeOptions.Value.CultureName);
    }

    public Task<JarvisPluginTurnOutcome?> TryEnhanceTurnAsync(
        JarvisPluginTurnContext context,
        CancellationToken cancellationToken) =>
        TryEnhanceTurnCoreAsync(context, cancellationToken);

    private async Task<JarvisPluginTurnOutcome?> TryEnhanceTurnCoreAsync(
        JarvisPluginTurnContext context,
        CancellationToken cancellationToken)
    {
        var userMessage = context.UserMessage;
        var assistantText = context.AssistantText;
        var toolsInvoked = context.TurnScope.ToolsInvokedThisTurn;

        if (InternetSearchAssist.ShouldRunDirectSearch(userMessage, toolsInvoked)
            && InternetSearchAssist.TryBuildSearchQuery(userMessage, out var searchQuery))
        {
            var directAnswer = await RunDirectSearchAsync(context, searchQuery, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrEmpty(directAnswer))
            {
                return new JarvisPluginTurnOutcome(directAnswer);
            }
        }

        if (InternetSearchAssist.ShouldNudgeSearchTool(userMessage, assistantText, toolsInvoked))
        {
            context.Logger.LogInformation(
                "Web search without SearchWeb tool; retrying with nudge (query: {Preview}).",
                userMessage.Length > 80 ? userMessage[..80] + "…" : userMessage);

            context.TurnScope.BeginUserTurn();
            var searchRetryHistory = CloneHistory(context.HistorySnapshot);
            searchRetryHistory.AddUserMessage(InternetSearchAssist.SearchToolNudgeUserMessage);

            assistantText = await context
                .InvokeAssistantAsync(searchRetryHistory, cancellationToken)
                .ConfigureAwait(false);
            context.AssistantText = assistantText;
            toolsInvoked = context.TurnScope.ToolsInvokedThisTurn;

            if (InternetSearchAssist.ShouldRunDirectSearch(userMessage, toolsInvoked)
                && InternetSearchAssist.TryBuildSearchQuery(userMessage, out searchQuery))
            {
                var directAnswer = await RunDirectSearchAsync(context, searchQuery, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrEmpty(directAnswer))
                {
                    return new JarvisPluginTurnOutcome(directAnswer);
                }
            }

            if (!string.IsNullOrEmpty(assistantText))
            {
                return new JarvisPluginTurnOutcome(assistantText);
            }
        }

        if (!BrowserResponseGuard.ShouldRetryWithoutTools(userMessage, assistantText, toolsInvoked))
        {
            return null;
        }

        context.Logger.LogWarning(
            "Suspicious web answer without MCP tools (user turn: {Preview}); retrying with nudge.",
            userMessage.Length > 80 ? userMessage[..80] + "…" : userMessage);

        context.TurnScope.BeginUserTurn();
        var retryHistory = CloneHistory(context.HistorySnapshot);
        retryHistory.AddUserMessage(BrowserResponseGuard.ToolNudgeUserMessage);

        var retryText = await context
            .InvokeAssistantAsync(retryHistory, cancellationToken)
            .ConfigureAwait(false);

        return new JarvisPluginTurnOutcome(retryText);
    }

    private async Task<string?> RunDirectSearchAsync(
        JarvisPluginTurnContext context,
        string searchQuery,
        CancellationToken cancellationToken)
    {
        if (!context.Kernel.Plugins.TryGetPlugin(SearchWebPluginMetadata.PluginName, out _))
        {
            _logger.LogWarning(
                "Plugin {PluginName} is not registered; direct search unavailable.",
                SearchWebPluginMetadata.PluginName);
            return null;
        }

        FunctionResult searchResult;
        try
        {
            searchResult = await context.Kernel
                .InvokeAsync(
                    SearchWebPluginMetadata.PluginName,
                    SearchWebPluginMetadata.SearchInternetFunction,
                    new KernelArguments
                    {
                        ["query"] = searchQuery,
                        ["maxRelatedTopics"] = 8,
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Direct internet search failed.");
            return null;
        }

        var raw = searchResult.GetValue<string>()?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        return await SummarizeSearchAsync(
                context,
                searchQuery,
                raw,
                _responseCulture,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string> SummarizeSearchAsync(
        JarvisPluginTurnContext context,
        string searchQuery,
        string searchResult,
        CultureInfo responseCulture,
        CancellationToken cancellationToken)
    {
        var languageName = responseCulture.NativeName;
        var synthesisPrompt =
            "User request: "
            + context.UserMessage
            + "\nWeb search query: "
            + searchQuery
            + "\nSearch result:\n"
            + searchResult
            + "\n\nSummarize briefly in "
            + languageName
            + " (suitable for voice). "
            + "Use only information from the result; if it is sparse or empty, say so clearly.";

        var synthesisHistory = CloneHistory(context.HistorySnapshot);
        synthesisHistory.AddUserMessage(synthesisPrompt);

        var synthesisSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = context.Temperature,
            FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
        };

        var response = await context.ChatCompletion
            .GetChatMessageContentAsync(
                synthesisHistory,
                synthesisSettings,
                kernel: context.Kernel,
                cancellationToken)
            .ConfigureAwait(false);

        var text = response.Content?.Trim();
        return string.IsNullOrEmpty(text) ? searchResult : text;
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
}
