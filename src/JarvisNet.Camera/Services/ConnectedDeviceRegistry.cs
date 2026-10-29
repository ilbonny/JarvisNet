using System.Globalization;
using System.Text.RegularExpressions;
using JarvisNet.Camera.Models;
using JarvisNet.Camera.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace JarvisNet.Camera.Services;

public sealed class ConnectedDeviceRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<string, DeviceEntry> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly IOptionsMonitor<CameraOptions> _cameraOptions;

    public ConnectedDeviceRegistry(IOptionsMonitor<CameraOptions> cameraOptions)
    {
        _cameraOptions = cameraOptions;
    }

    public event EventHandler? Changed;

    public void RecordHeartbeat(HttpContext httpContext, string? typeOverride, string? nameOverride, string? ipOverride)
    {
        var identity = ResolveIdentity(httpContext, typeOverride ?? "camera", nameOverride, ipOverride);
        Touch(identity, motion: false, ingestAccepted: null);
    }

    public void RecordMotion(HttpContext httpContext, bool? ingestAccepted)
    {
        var identity = ResolveIdentity(httpContext, "camera", null, null);
        Touch(identity, motion: true, ingestAccepted);
    }

    public bool? GetMotionDetection(string deviceId)
    {
        lock (_lock)
        {
            return _devices.TryGetValue(deviceId, out var entry) ? entry.MotionDetectionEnabled : null;
        }
    }

    public void SetMotionDetection(string deviceId, bool enabled)
    {
        lock (_lock)
        {
            if (!_devices.TryGetValue(deviceId, out var entry))
            {
                return;
            }

            entry.MotionDetectionEnabled = enabled;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<ConnectedDeviceSnapshot> GetSnapshots()
    {
        var onlineSeconds = Math.Max(30, _cameraOptions.CurrentValue.DeviceOnlineSeconds);
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            return _devices.Values
                .Select(entry => ToSnapshot(entry, now, onlineSeconds))
                .OrderBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private void Touch(DeviceIdentity identity, bool motion, bool? ingestAccepted)
    {
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            var entry = FindOrMergeEntry(identity, now);

            entry.HostLabel = identity.DisplayName;
            EnsureFriendlyDisplayName(entry);
            entry.IpAddress = identity.IpAddress;
            entry.LastSeenUtc = now;

            if (motion)
            {
                entry.LastMotionUtc = now;
                if (ingestAccepted == true)
                {
                    entry.LastIngestSuccessUtc = now;
                }
                else if (ingestAccepted == false)
                {
                    entry.LastIngestFailureUtc = now;
                }
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private DeviceEntry FindOrMergeEntry(DeviceIdentity identity, DateTimeOffset now)
    {
        if (_devices.TryGetValue(identity.Id, out var direct))
        {
            RemoveDuplicateEntries(identity, keepId: identity.Id);
            return direct;
        }

        var mergedFrom = FindDuplicateEntry(identity);
        if (mergedFrom is not null)
        {
            var merged = MergeEntries(mergedFrom, identity, now);
            _devices.Remove(mergedFrom.Id);
            _devices[identity.Id] = merged;
            RemoveDuplicateEntries(identity, keepId: identity.Id);
            return merged;
        }

        var entry = new DeviceEntry
        {
            Id = identity.Id,
            Type = identity.Type,
            HostLabel = identity.DisplayName,
            FriendlyDisplayName = string.Empty,
            IpAddress = identity.IpAddress,
            FirstSeenUtc = now,
            MotionDetectionEnabled = _cameraOptions.CurrentValue.DefaultMotionDetectionEnabled,
        };
        EnsureFriendlyDisplayName(entry);
        _devices[identity.Id] = entry;
        return entry;
    }

    private void EnsureFriendlyDisplayName(DeviceEntry entry)
    {
        if (!string.Equals(entry.Type, "camera", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(entry.FriendlyDisplayName))
            {
                entry.FriendlyDisplayName = entry.HostLabel;
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(entry.FriendlyDisplayName))
        {
            return;
        }

        entry.FriendlyDisplayName = AllocateCameraFriendlyName();
    }

    private string AllocateCameraFriendlyName()
    {
        var used = new HashSet<int>();
        foreach (var device in _devices.Values)
        {
            if (!string.Equals(device.Type, "camera", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryParseCameraNumber(device.FriendlyDisplayName, out var n))
            {
                used.Add(n);
            }
        }

        var next = 1;
        while (used.Contains(next))
        {
            next++;
        }

        return FormattableString.Invariant($"CAM{next}");
    }

    private static bool TryParseCameraNumber(string label, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var match = Regex.Match(label.Trim(), "^CAM(\\d+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        return int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private DeviceEntry? FindDuplicateEntry(DeviceIdentity identity)
    {
        if (!HasUsableIp(identity.IpAddress))
        {
            return null;
        }

        foreach (var candidate in _devices.Values)
        {
            if (candidate.Id == identity.Id)
            {
                continue;
            }

            if (!string.Equals(candidate.Type, identity.Type, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(candidate.IpAddress, identity.IpAddress, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private void RemoveDuplicateEntries(DeviceIdentity identity, string keepId)
    {
        if (!HasUsableIp(identity.IpAddress))
        {
            return;
        }

        var staleIds = _devices.Values
            .Where(d => !string.Equals(d.Id, keepId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.Type, identity.Type, StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.IpAddress, identity.IpAddress, StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Id)
            .ToList();

        foreach (var staleId in staleIds)
        {
            _devices.Remove(staleId);
        }
    }

    private static DeviceEntry MergeEntries(DeviceEntry existing, DeviceIdentity identity, DateTimeOffset now)
    {
        return new DeviceEntry
        {
            Id = identity.Id,
            Type = identity.Type,
            HostLabel = identity.DisplayName,
            FriendlyDisplayName = existing.FriendlyDisplayName,
            IpAddress = identity.IpAddress,
            FirstSeenUtc = existing.FirstSeenUtc,
            LastSeenUtc = existing.LastSeenUtc > now ? existing.LastSeenUtc : now,
            LastMotionUtc = existing.LastMotionUtc,
            LastIngestSuccessUtc = existing.LastIngestSuccessUtc,
            LastIngestFailureUtc = existing.LastIngestFailureUtc,
            MotionDetectionEnabled = existing.MotionDetectionEnabled,
        };
    }

    private static bool HasUsableIp(string ip) =>
        !string.IsNullOrWhiteSpace(ip) && !string.Equals(ip, "—", StringComparison.Ordinal);

    private static ConnectedDeviceSnapshot ToSnapshot(DeviceEntry entry, DateTimeOffset now, int onlineSeconds)
    {
        var online = (now - entry.LastSeenUtc).TotalSeconds <= onlineSeconds;
        var degraded = online
            && entry.LastIngestFailureUtc.HasValue
            && (!entry.LastIngestSuccessUtc.HasValue
                || entry.LastIngestFailureUtc > entry.LastIngestSuccessUtc);

        var status = !online ? "offline" : degraded ? "degraded" : "online";
        var operational = online && !degraded;

        return new ConnectedDeviceSnapshot
        {
            Id = entry.Id,
            Type = entry.Type,
            DisplayName = entry.FriendlyDisplayName,
            HostLabel = entry.HostLabel,
            IpAddress = entry.IpAddress,
            FirstSeenUtc = entry.FirstSeenUtc,
            LastSeenUtc = entry.LastSeenUtc,
            LastMotionUtc = entry.LastMotionUtc,
            LastIngestSuccessUtc = entry.LastIngestSuccessUtc,
            LastIngestFailureUtc = entry.LastIngestFailureUtc,
            Status = status,
            IsOnline = online,
            IsOperational = operational,
            MotionDetectionEnabled = entry.MotionDetectionEnabled,
        };
    }

    private static DeviceIdentity ResolveIdentity(
        HttpContext httpContext,
        string type,
        string? nameOverride,
        string? ipOverride)
    {
        var request = httpContext.Request;
        var name = FirstNonEmpty(
            nameOverride,
            request.Headers["X-Device-Name"].FirstOrDefault(),
            request.Headers["X-Device-Id"].FirstOrDefault(),
            "ESP32-CAM");

        var ip = FirstNonEmpty(
            ipOverride,
            request.Headers["X-Device-Ip"].FirstOrDefault(),
            ResolveClientIp(httpContext));

        var stableId = FirstNonEmpty(
            request.Headers["X-Device-Id"].FirstOrDefault(),
            request.Headers["X-Device-Name"].FirstOrDefault(),
            nameOverride);

        var id = IsGenericLabel(stableId)
            ? $"{type}:{ip}"
            : stableId;

        return new DeviceIdentity(id, type, name, ip);
    }

    private static string ResolveClientIp(HttpContext httpContext)
    {
        var remote = httpContext.Connection.RemoteIpAddress;
        if (remote is null)
        {
            return "—";
        }

        if (remote.IsIPv4MappedToIPv6)
        {
            remote = remote.MapToIPv4();
        }

        return remote.ToString();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "—";
    }

    private static bool IsGenericLabel(string value) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(value, "—", StringComparison.Ordinal)
        || string.Equals(value, "ESP32-CAM", StringComparison.OrdinalIgnoreCase);

    private sealed class DeviceEntry
    {
        public required string Id { get; init; }

        public required string Type { get; init; }

        public string HostLabel { get; set; } = string.Empty;

        public string FriendlyDisplayName { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;

        public DateTimeOffset FirstSeenUtc { get; init; }

        public DateTimeOffset LastSeenUtc { get; set; }

        public DateTimeOffset? LastMotionUtc { get; set; }

        public DateTimeOffset? LastIngestSuccessUtc { get; set; }

        public DateTimeOffset? LastIngestFailureUtc { get; set; }

        public bool MotionDetectionEnabled { get; set; }
    }

    private readonly record struct DeviceIdentity(string Id, string Type, string DisplayName, string IpAddress);
}
