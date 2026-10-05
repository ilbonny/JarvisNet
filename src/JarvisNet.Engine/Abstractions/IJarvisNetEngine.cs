using JarvisNet.Core.Enums;
using Microsoft.SemanticKernel;

namespace JarvisNet.Engine.Abstractions;

public interface IJarvisNetEngine
{
    event EventHandler<string>? UserTranscriptReceived;

    event EventHandler<string>? AssistantResponseReceived;

    event EventHandler<string>? AssistantResponseChunk;

    event EventHandler<JarvisState>? StateChanged;

    event EventHandler<float>? AudioLevelChanged;

    event EventHandler<string>? McpToolExecuting;

    Task<string> SendAsync(string userMessage, CancellationToken cancellationToken = default);

    void ClearHistory();

    IReadOnlyList<ChatMessageContent> GetHistorySnapshot();

    Task StartVoiceSessionAsync(CancellationToken cancellationToken = default);

    Task StopVoiceSessionAsync(CancellationToken cancellationToken = default);
}
