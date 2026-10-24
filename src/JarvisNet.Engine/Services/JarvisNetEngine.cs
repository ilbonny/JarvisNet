using JarvisNet.Core.Enums;
using JarvisNet.Engine.Abstractions;
using JarvisNet.Engine.Models;
using Microsoft.SemanticKernel;

namespace JarvisNet.Engine.Services;

public sealed class JarvisNetEngine : IJarvisNetEngine
{
    private readonly OllamaBrainService _brain;
    private readonly JarvisNetOrchestrator _orchestrator;
    private readonly JarvisTurnScope _turnScope;

    public JarvisNetEngine(
        OllamaBrainService brain,
        JarvisNetOrchestrator orchestrator,
        JarvisTurnScope turnScope)
    {
        _brain = brain;
        _orchestrator = orchestrator;
        _turnScope = turnScope;
        _orchestrator.UserTranscriptReceived += (_, text) => UserTranscriptReceived?.Invoke(this, text);
        _orchestrator.AssistantResponseReceived += (_, text) => AssistantResponseReceived?.Invoke(this, text);
        _orchestrator.AssistantResponseChunk += (_, chunk) => AssistantResponseChunk?.Invoke(this, chunk);
        _orchestrator.StateChanged += (_, state) => StateChanged?.Invoke(this, state);
        _orchestrator.AudioLevelChanged += (_, level) => AudioLevelChanged?.Invoke(this, level);
        _turnScope.ModuleToolExecutionStarted += (_, args) =>
        {
            McpToolExecuting?.Invoke(this, args.DisplayName);
            ModuleToolExecutionStarted?.Invoke(this, args);
        };
        _turnScope.ModuleToolExecutionCompleted += (_, args) =>
            ModuleToolExecutionCompleted?.Invoke(this, args);
    }

    public event EventHandler<string>? UserTranscriptReceived;

    public event EventHandler<string>? AssistantResponseReceived;

    public event EventHandler<string>? AssistantResponseChunk;

    public event EventHandler<JarvisState>? StateChanged;

    public event EventHandler<float>? AudioLevelChanged;

    public event EventHandler<string>? McpToolExecuting;

    public event EventHandler<ModuleToolEventArgs>? ModuleToolExecutionStarted;

    public event EventHandler<ModuleToolEventArgs>? ModuleToolExecutionCompleted;

    public Task<string> SendAsync(string userMessage, CancellationToken cancellationToken = default) =>
        _brain.SendAsync(userMessage, cancellationToken);

    public void ClearHistory() => _brain.ClearHistory();

    public IReadOnlyList<ChatMessageContent> GetHistorySnapshot() => _brain.GetHistorySnapshot();

    public Task StartVoiceSessionAsync(CancellationToken cancellationToken = default) =>
        _orchestrator.StartVoiceSessionAsync(cancellationToken);

    public Task StopVoiceSessionAsync(CancellationToken cancellationToken = default) =>
        _orchestrator.StopVoiceSessionAsync(cancellationToken);
}
