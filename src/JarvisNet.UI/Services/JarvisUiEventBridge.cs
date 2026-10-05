using JarvisNet.Core.Enums;
using JarvisNet.Engine.Abstractions;
using JarvisNet.UI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace JarvisNet.UI.Services;

public sealed class JarvisUiEventBridge : IHostedService
{
    private readonly IHubContext<JarvisHub> _hubContext;
    private readonly IJarvisNetEngine _engine;

    public JarvisUiEventBridge(IHubContext<JarvisHub> hubContext, IJarvisNetEngine engine)
    {
        _hubContext = hubContext;
        _engine = engine;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _engine.StateChanged += OnStateChanged;
        _engine.AudioLevelChanged += OnAudioLevelChanged;
        _engine.UserTranscriptReceived += OnUserTranscriptReceived;
        _engine.AssistantResponseChunk += OnAssistantResponseChunk;
        _engine.AssistantResponseReceived += OnAssistantResponseReceived;
        _engine.McpToolExecuting += OnMcpToolExecuting;

        return _hubContext.Clients.All.SendAsync("StateChanged", JarvisState.Idle.ToString(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _engine.StateChanged -= OnStateChanged;
        _engine.AudioLevelChanged -= OnAudioLevelChanged;
        _engine.UserTranscriptReceived -= OnUserTranscriptReceived;
        _engine.AssistantResponseChunk -= OnAssistantResponseChunk;
        _engine.AssistantResponseReceived -= OnAssistantResponseReceived;
        _engine.McpToolExecuting -= OnMcpToolExecuting;

        return Task.CompletedTask;
    }

    private void OnStateChanged(object? sender, JarvisState state) =>
        _ = _hubContext.Clients.All.SendAsync("StateChanged", state.ToString());

    private void OnAudioLevelChanged(object? sender, float level) =>
        _ = _hubContext.Clients.All.SendAsync("AudioLevel", level);

    private void OnUserTranscriptReceived(object? sender, string text) =>
        _ = _hubContext.Clients.All.SendAsync("UserSpeech", text);

    private void OnAssistantResponseChunk(object? sender, string chunk) =>
        _ = _hubContext.Clients.All.SendAsync("JarvisChunk", chunk);

    private void OnAssistantResponseReceived(object? sender, string text) =>
        _ = _hubContext.Clients.All.SendAsync("JarvisChunkEnd");

    private void OnMcpToolExecuting(object? sender, string toolName) =>
        _ = _hubContext.Clients.All.SendAsync("McpToolExecuting", toolName);
}
