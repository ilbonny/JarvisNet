using JarvisNet.Camera.Models;

namespace JarvisNet.Camera.Services;

public interface ICameraTurnMediaBuffer
{
    void Enqueue(CameraTurnMediaItem item);

    IReadOnlyList<CameraTurnMediaItem> TakeAll();
}
