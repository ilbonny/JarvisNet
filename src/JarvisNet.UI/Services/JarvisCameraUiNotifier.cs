using JarvisNet.Camera.Abstractions;
using JarvisNet.Camera.Models;
using JarvisNet.UI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace JarvisNet.UI.Services;

public sealed class JarvisCameraUiNotifier : IJarvisCameraUiNotifier
{
    private readonly IHubContext<JarvisHub> _hubContext;

    public JarvisCameraUiNotifier(IHubContext<JarvisHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyMediaAsync(CameraTurnMediaItem item, string mediaUrl, CancellationToken cancellationToken) =>
        _hubContext.Clients.All.SendAsync(
            "JarvisMedia",
            new
            {
                kind = item.Kind.ToString().ToLowerInvariant(),
                url = mediaUrl,
                caption = item.Caption,
                deviceName = item.DeviceName,
            },
            cancellationToken);
}
