using System.Text.Json;
using JarvisNet.Camera.Options;
using JarvisNet.Camera.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace JarvisNet.Camera.Endpoints;

public static class CameraMotionEndpoints
{
    public const string MotionRoute = "/api/camera/motion";
    public const string HeartbeatRoute = "/api/camera/heartbeat";
    public const string DevicesRoute = "/api/devices";
    public const string MediaRoute = "/api/camera/media/{mediaId}";

    public static IEndpointRouteBuilder MapJarvisNetCameraEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(MotionRoute, HandleMotionAsync)
            .DisableAntiforgery();

        endpoints.MapPost(HeartbeatRoute, HandleHeartbeatAsync)
            .DisableAntiforgery();

        endpoints.MapGet(DevicesRoute, HandleGetDevices);

        endpoints.MapGet(MediaRoute, HandleGetMedia);

        return endpoints;
    }

    private static IResult HandleGetDevices(
        ConnectedDeviceRegistry registry,
        IOptionsMonitor<CameraOptions> cameraOptions)
    {
        var options = cameraOptions.CurrentValue;
        var devices = registry.GetSnapshots()
            .Select(d => new
            {
                id = d.Id,
                type = d.Type,
                displayName = d.DisplayName,
                hostLabel = d.HostLabel,
                ipAddress = d.IpAddress,
                firstSeenUtc = d.FirstSeenUtc,
                lastSeenUtc = d.LastSeenUtc,
                lastMotionUtc = d.LastMotionUtc,
                status = d.Status,
                isOnline = d.IsOnline,
                isOperational = d.IsOperational,
                motionDetectionEnabled = d.MotionDetectionEnabled,
            });

        return Results.Ok(new
        {
            onlineThresholdSeconds = options.DeviceOnlineSeconds,
            devices,
        });
    }

    private static async Task<IResult> HandleHeartbeatAsync(
        HttpContext httpContext,
        ConnectedDeviceRegistry registry,
        IOptionsMonitor<CameraOptions> cameraOptions,
        CancellationToken cancellationToken)
    {
        var options = cameraOptions.CurrentValue;
        var auth = CameraDeviceAuth.ValidateIngest(httpContext, options);
        if (auth is not null)
        {
            return auth;
        }

        string? type = null;
        string? name = null;
        string? ip = null;

        if (httpContext.Request.ContentLength is > 0
            && httpContext.Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                var payload = await JsonSerializer.DeserializeAsync<HeartbeatPayload>(
                        httpContext.Request.Body,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                type = payload?.Type;
                name = payload?.Name;
                ip = payload?.Ip;
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON body." });
            }
        }

        registry.RecordHeartbeat(httpContext, type, name, ip);
        var snapshots = registry.GetSnapshots();
        var device = snapshots.FirstOrDefault(d =>
            !string.IsNullOrWhiteSpace(name)
                && (string.Equals(d.HostLabel, name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(d.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
            ?? snapshots.FirstOrDefault(d =>
                !string.IsNullOrWhiteSpace(ip)
                    && string.Equals(d.IpAddress, ip, StringComparison.OrdinalIgnoreCase));

        var motionDetectionEnabled = device?.MotionDetectionEnabled
            ?? options.DefaultMotionDetectionEnabled;

        return Results.Ok(new { status = "registered", motionDetectionEnabled });
    }

    private static IResult HandleGetMedia(string mediaId, CameraMediaCache cache)
    {
        var entry = cache.TryGet(mediaId);
        if (entry is null)
        {
            return Results.NotFound();
        }

        return Results.File(entry.Data, entry.ContentType, entry.FileName);
    }

    private static async Task<IResult> HandleMotionAsync(
        HttpContext httpContext,
        CameraMotionAlertService alerts,
        ConnectedDeviceRegistry registry,
        IOptionsMonitor<CameraOptions> cameraOptions,
        CancellationToken cancellationToken)
    {
        var options = cameraOptions.CurrentValue;
        var auth = CameraDeviceAuth.ValidateIngest(httpContext, options);
        if (auth is not null)
        {
            return auth;
        }

        var (stream, fileName) = await TryReadImageAsync(httpContext.Request, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            return Results.BadRequest(new { error = "Expected JPEG body (Content-Type: image/jpeg) or multipart field 'image' / 'file'." });
        }

        await using (stream)
        {
            var caption = httpContext.Request.Query.TryGetValue("caption", out var cap)
                ? cap.ToString()
                : null;

            var (accepted, error) = await alerts.TrySendMotionPhotoAsync(
                    stream,
                    fileName,
                    caption,
                    cancellationToken)
                .ConfigureAwait(false);

            var cooldown = error?.Contains("Cooldown", StringComparison.OrdinalIgnoreCase) == true;
            registry.RecordMotion(
                httpContext,
                accepted ? true : cooldown ? null : false);

            if (!accepted)
            {
                var status = cooldown
                    ? StatusCodes.Status429TooManyRequests
                    : StatusCodes.Status503ServiceUnavailable;
                return Results.Json(new { error }, statusCode: status);
            }

            return Results.Ok(new { status = "sent" });
        }
    }

    private static async Task<(Stream? Stream, string FileName)> TryReadImageAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            var file = form.Files.GetFile("image") ?? form.Files.GetFile("file");
            if (file is { Length: > 0 })
            {
                var memory = new MemoryStream();
                await file.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
                memory.Position = 0;
                var name = string.IsNullOrWhiteSpace(file.FileName) ? "motion.jpg" : file.FileName;
                return (memory, name);
            }
        }

        var contentType = request.ContentType ?? string.Empty;
        if (contentType.StartsWith("image/jpeg", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            var memory = new MemoryStream();
            await request.Body.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            if (memory.Length == 0)
            {
                await memory.DisposeAsync().ConfigureAwait(false);
                return (null, "motion.jpg");
            }

            memory.Position = 0;
            return (memory, "motion.jpg");
        }

        return (null, "motion.jpg");
    }

    private sealed class HeartbeatPayload
    {
        public string? Type { get; set; }

        public string? Name { get; set; }

        public string? Ip { get; set; }
    }
}
