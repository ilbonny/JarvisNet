using JarvisNet.Camera.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace JarvisNet.Camera.Endpoints;

internal static class CameraDeviceAuth
{
    internal static IResult? ValidateIngest(HttpContext httpContext, CameraOptions options)
    {
        if (!options.Enabled)
        {
            return Results.Json(
                new { error = "Camera ingest disabled." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (options.IsIngestProtected)
        {
            if (!httpContext.Request.Headers.TryGetValue("X-Api-Key", out var provided)
                || !string.Equals(provided.ToString().Trim(), options.ResolvedIngestApiKey, StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }
        }

        return null;
    }
}
