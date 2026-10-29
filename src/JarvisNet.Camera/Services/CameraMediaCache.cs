namespace JarvisNet.Camera.Services;

public sealed class CameraMediaCache
{
    private readonly object _lock = new();
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(2);

    public string Store(byte[] data, string contentType, string fileName)
    {
        var id = Guid.NewGuid().ToString("N");
        lock (_lock)
        {
            PurgeExpired_NoLock();
            _entries[id] = new CacheEntry(data, contentType, fileName, DateTimeOffset.UtcNow);
        }

        return id;
    }

    public CacheEntry? TryGet(string id)
    {
        lock (_lock)
        {
            PurgeExpired_NoLock();
            return _entries.TryGetValue(id, out var entry) ? entry : null;
        }
    }

    private void PurgeExpired_NoLock()
    {
        var cutoff = DateTimeOffset.UtcNow - Ttl;
        var stale = _entries.Where(kv => kv.Value.StoredUtc < cutoff).Select(kv => kv.Key).ToList();
        foreach (var key in stale)
        {
            _entries.Remove(key);
        }
    }

    public sealed record CacheEntry(byte[] Data, string ContentType, string FileName, DateTimeOffset StoredUtc);
}
