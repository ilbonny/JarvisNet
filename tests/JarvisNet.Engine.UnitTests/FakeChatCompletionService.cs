using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JarvisNet.Engine.UnitTests;

internal sealed class FakeChatCompletionService : IChatCompletionService
{
    private readonly Func<ChatHistory, ChatMessageContent> _responder;

    public FakeChatCompletionService(Func<ChatHistory, ChatMessageContent> responder)
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
        return Task.FromResult(_responder(chatHistory));
    }

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ChatMessageContent>>([_responder(chatHistory)]);
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
