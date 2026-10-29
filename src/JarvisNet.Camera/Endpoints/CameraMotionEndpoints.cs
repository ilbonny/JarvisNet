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

    public static IEndpointRouteBuilder MapJarvisNetCameraEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(MotionRoute, HandleMotionAsync)
            .DisableAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> HandleMotionAsync(
        HttpContext httpContext,
        CameraMotionAlertService alerts,
        IOptionsMonitor<CameraOptions> cameraOptions,
        CancellationToken cancellationToken)
    {
        var options = cameraOptions.CurrentValue;
        if (!options.Enabled)
        {
            return Results.Json(new { error = "Camera ingest disabled." }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (options.IsIngestProtected)
        {
            if (!httpContext.Request.Headers.TryGetValue("X-Api-Key", out var provided)
                || provided != options.IngestApiKey)
            {
                return Results.Unauthorized();
            }
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

            if (!accepted)
            {
                var status = error?.Contains("Cooldown", StringComparison.OrdinalIgnoreCase) == true
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
}
