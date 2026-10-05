using JarvisNet.Core.Enums;
using JarvisNet.Engine.Abstractions;
using Microsoft.SemanticKernel;

namespace JarvisNet.Engine.Services;

public sealed class JarvisNetEngine : IJarvisNetEngine
{
    private readonly OllamaBrainService _brain;
    private readonly JarvisNetOrchestrator _orchestrator;
    private readonly McpClientManager _mcpClientManager;

    public JarvisNetEngine(
        OllamaBrainService brain,
        JarvisNetOrchestrator orchestrator,
        McpClientManager mcpClientManager)
    {
        _brain = brain;
        _orchestrator = orchestrator;
        _mcpClientManager = mcpClientManager;
        _orchestrator.UserTranscriptReceived += (_, text) => UserTranscriptReceived?.Invoke(this, text);
        _orchestrator.AssistantResponseReceived += (_, text) => AssistantResponseReceived?.Invoke(this, text);
        _orchestrator.AssistantResponseChunk += (_, chunk) => AssistantResponseChunk?.Invoke(this, chunk);
        _orchestrator.StateChanged += (_, state) => StateChanged?.Invoke(this, state);
        _orchestrator.AudioLevelChanged += (_, level) => AudioLevelChanged?.Invoke(this, level);
        _mcpClientManager.McpToolExecutionStarted += (_, toolName) =>
            McpToolExecuting?.Invoke(this, toolName);
    }

    public event EventHandler<string>? UserTranscriptReceived;

    public event EventHandler<string>? AssistantResponseReceived;

    public event EventHandler<string>? AssistantResponseChunk;

    public event EventHandler<JarvisState>? StateChanged;

    public event EventHandler<float>? AudioLevelChanged;

    public event EventHandler<string>? McpToolExecuting;

    public Task<string> SendAsync(string userMessage, CancellationToken cancellationToken = default) =>
        _brain.SendAsync(userMessage, cancellationToken);

    public void ClearHistory() => _brain.ClearHistory();

    public IReadOnlyList<ChatMessageContent> GetHistorySnapshot() => _brain.GetHistorySnapshot();

    public Task StartVoiceSessionAsync(CancellationToken cancellationToken = default) =>
        _orchestrator.StartVoiceSessionAsync(cancellationToken);

    public Task StopVoiceSessionAsync(CancellationToken cancellationToken = default) =>
        _orchestrator.StopVoiceSessionAsync(cancellationToken);
}
