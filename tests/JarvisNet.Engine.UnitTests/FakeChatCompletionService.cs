using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JarvisNet.Engine.UnitTests;

/// <summary>
/// Fake IChatCompletionService for unit tests.
/// Captures the last call's kernel and execution settings for assertion.
/// </summary>
internal sealed class FakeChatCompletionService : IChatCompletionService
{
    private readonly Func<ChatHistory, PromptExecutionSettings?, Kernel?, ChatMessageContent> _responder;

    public Kernel? LastKernel { get; private set; }
    public PromptExecutionSettings? LastExecutionSettings { get; private set; }
    public int CallCount { get; private set; }

    public FakeChatCompletionService(Func<ChatHistory, ChatMessageContent> responder)
        : this((history, _, _) => responder(history))
    {
    }

    public FakeChatCompletionService(
        Func<ChatHistory, PromptExecutionSettings?, Kernel?, ChatMessageContent> responder)
    {
        _responder = responder;
    }

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public Task<ChatMessageContent> GetChatMessageContentAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        LastKernel = kernel;
        LastExecutionSettings = executionSettings;
        CallCount++;
        return Task.FromResult(_responder(chatHistory, executionSettings, kernel));
    }

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        LastKernel = kernel;
        LastExecutionSettings = executionSettings;
        CallCount++;
        return Task.FromResult<IReadOnlyList<ChatMessageContent>>(
            [_responder(chatHistory, executionSettings, kernel)]);
    }

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
