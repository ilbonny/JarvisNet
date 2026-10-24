using JarvisNet.Core.Enums;
using JarvisNet.Engine.Abstractions;
using JarvisNet.Engine.Models;
using JarvisNet.Engine.Services;
using JarvisNet.UI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace JarvisNet.UI.Services;

public sealed class JarvisUiEventBridge : IHostedService
{
    private readonly IHubContext<JarvisHub> _hubContext;
    private readonly IJarvisNetEngine _engine;
    private readonly JarvisModuleCatalogService _moduleCatalog;

    public JarvisUiEventBridge(
        IHubContext<JarvisHub> hubContext,
        IJarvisNetEngine engine,
        JarvisModuleCatalogService moduleCatalog)
    {
        _hubContext = hubContext;
        _engine = engine;
        _moduleCatalog = moduleCatalog;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _engine.StateChanged += OnStateChanged;
        _engine.AudioLevelChanged += OnAudioLevelChanged;
        _engine.UserTranscriptReceived += OnUserTranscriptReceived;
        _engine.AssistantResponseChunk += OnAssistantResponseChunk;
        _engine.AssistantResponseReceived += OnAssistantResponseReceived;
        _engine.McpToolExecuting += OnMcpToolExecuting;
        _engine.ModuleToolExecutionStarted += OnModuleToolExecutionStarted;
        _engine.ModuleToolExecutionCompleted += OnModuleToolExecutionCompleted;
        _moduleCatalog.CatalogChanged += OnModuleCatalogChanged;

        await _hubContext.Clients.All.SendAsync("StateChanged", JarvisState.Idle.ToString(), cancellationToken)
            .ConfigureAwait(false);
        await BroadcastModulesCatalogAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _engine.StateChanged -= OnStateChanged;
        _engine.AudioLevelChanged -= OnAudioLevelChanged;
        _engine.UserTranscriptReceived -= OnUserTranscriptReceived;
        _engine.AssistantResponseChunk -= OnAssistantResponseChunk;
        _engine.AssistantResponseReceived -= OnAssistantResponseReceived;
        _engine.McpToolExecuting -= OnMcpToolExecuting;
        _engine.ModuleToolExecutionStarted -= OnModuleToolExecutionStarted;
        _engine.ModuleToolExecutionCompleted -= OnModuleToolExecutionCompleted;
        _moduleCatalog.CatalogChanged -= OnModuleCatalogChanged;

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

    private void OnModuleToolExecutionStarted(object? sender, ModuleToolEventArgs args) =>
        _ = _hubContext.Clients.All.SendAsync(
            "ModuleToolStarted",
            new
            {
                moduleId = args.ModuleId,
                toolName = args.ToolName,
                displayName = args.DisplayName,
            });

    private void OnModuleToolExecutionCompleted(object? sender, ModuleToolEventArgs args) =>
        _ = _hubContext.Clients.All.SendAsync(
            "ModuleToolFinished",
            new
            {
                moduleId = args.ModuleId,
                toolName = args.ToolName,
                displayName = args.DisplayName,
            });

    private void OnModuleCatalogChanged(object? sender, EventArgs e) =>
        _ = BroadcastModulesCatalogAsync(CancellationToken.None);

    private Task BroadcastModulesCatalogAsync(CancellationToken cancellationToken)
    {
        var modules = _moduleCatalog.GetModules()
            .Select(m => new
            {
                id = m.Id,
                kind = m.Kind,
                toolCount = m.ToolCount,
                description = m.Description,
            })
            .ToList();

        return _hubContext.Clients.All.SendAsync("ModulesCatalog", modules, cancellationToken);
    }
}
