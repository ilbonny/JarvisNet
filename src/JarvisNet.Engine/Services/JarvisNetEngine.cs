using JarvisNet.Engine.Abstractions;
using Microsoft.SemanticKernel;

namespace JarvisNet.Engine.Services;

public sealed class JarvisNetEngine : IJarvisNetEngine
{
    private readonly OllamaBrainService _brain;
    private readonly JarvisNetOrchestrator _orchestrator;

    public JarvisNetEngine(OllamaBrainService brain, JarvisNetOrchestrator orchestrator)
    {
        _brain = brain;
        _orchestrator = orchestrator;
        _orchestrator.UserTranscriptReceived += (_, text) => UserTranscriptReceived?.Invoke(this, text);
        _orchestrator.AssistantResponseReceived += (_, text) => AssistantResponseReceived?.Invoke(this, text);
    }

    public event EventHandler<string>? UserTranscriptReceived;

    public event EventHandler<string>? AssistantResponseReceived;

    public Task<string> SendAsync(string userMessage, CancellationToken cancellationToken = default) =>
        _brain.SendAsync(userMessage, cancellationToken);

    public void ClearHistory() => _brain.ClearHistory();

    public IReadOnlyList<ChatMessageContent> GetHistorySnapshot() => _brain.GetHistorySnapshot();

    public Task StartVoiceSessionAsync(CancellationToken cancellationToken = default) =>
        _orchestrator.StartVoiceSessionAsync(cancellationToken);

    public Task StopVoiceSessionAsync(CancellationToken cancellationToken = default) =>
        _orchestrator.StopVoiceSessionAsync(cancellationToken);
}
