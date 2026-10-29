using JarvisNet.Camera.Models;

namespace JarvisNet.Camera.Services;

public sealed class CameraTurnMediaBuffer : ICameraTurnMediaBuffer
{
    private readonly object _lock = new();
    private readonly List<CameraTurnMediaItem> _items = [];

    public void Enqueue(CameraTurnMediaItem item)
    {
        lock (_lock)
        {
            _items.Add(item);
        }
    }

    public IReadOnlyList<CameraTurnMediaItem> TakeAll()
    {
        lock (_lock)
        {
            if (_items.Count == 0)
            {
                return [];
            }

            var copy = _items.ToList();
            _items.Clear();
            return copy;
        }
    }
}
