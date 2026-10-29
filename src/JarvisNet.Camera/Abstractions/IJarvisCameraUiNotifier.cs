using JarvisNet.Camera.Models;

namespace JarvisNet.Camera.Abstractions;

public interface IJarvisCameraUiNotifier
{
    Task NotifyMediaAsync(CameraTurnMediaItem item, string mediaUrl, CancellationToken cancellationToken);
}
