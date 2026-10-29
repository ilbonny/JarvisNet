using JarvisNet.Camera.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JarvisNet.Camera.Services;

public sealed class CameraDeviceHttpClient
{
    public const string HttpClientName = "JarvisNet.Camera.Device";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<CameraOptions> _options;
    private readonly ILogger<CameraDeviceHttpClient> _logger;

    public CameraDeviceHttpClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<CameraOptions> options,
        ILogger<CameraDeviceHttpClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<byte[]?> TryCapturePhotoAsync(string ipAddress, CancellationToken cancellationToken)
    {
        var url = BuildControlUrl(ipAddress, "/capture");
        using var request = CreateRequest(HttpMethod.Get, url);
        return await SendForBytesAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]?> TryCaptureClipAsync(
        string ipAddress,
        int seconds,
        CancellationToken cancellationToken)
    {
        seconds = Math.Clamp(seconds, 1, _options.CurrentValue.MaxVideoSeconds);
        var url = BuildControlUrl(
            ipAddress,
            $"/clip?seconds={seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        using var request = CreateRequest(HttpMethod.Get, url);
        return await SendForBytesAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TrySetMotionDetectionAsync(
        string ipAddress,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var url = BuildControlUrl(ipAddress, "/motion");
        using var request = CreateRequest(HttpMethod.Put, url);
        request.Content = new StringContent(
            enabled ? "{\"enabled\":true}" : "{\"enabled\":false}",
            System.Text.Encoding.UTF8,
            "application/json");

        try
        {
            var client = CreateClient();
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Device motion PUT {Url} failed: {Status}.",
                    url,
                    (int)response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Device motion PUT failed for {Url}.", url);
            return false;
        }
    }

    private async Task<byte[]?> SendForBytesAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateClient();
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning(
                        "Device request {Method} {Url} returned 401. "
                        + "Verifica che JARVIS_API_KEY in esp32/secrets.h coincida con IngestApiKey in config/jarvisnet.camera.secrets.json.",
                        request.Method,
                        request.RequestUri);
                }
                else
                {
                    _logger.LogWarning(
                        "Device request {Method} {Url} failed: {Status}.",
                        request.Method,
                        request.RequestUri,
                        (int)response.StatusCode);
                }

                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Device request failed for {Url}.", request.RequestUri);
            return null;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        var apiKey = _options.CurrentValue.ResolvedIngestApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        }

        return request;
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient(HttpClientName);

    private string BuildControlUrl(string ipAddress, string pathAndQuery)
    {
        var port = _options.CurrentValue.DeviceControlPort;
        var host = ipAddress.Trim();
        var baseUrl = port == 80
            ? $"http://{host}{pathAndQuery}"
            : $"http://{host}:{port.ToString(System.Globalization.CultureInfo.InvariantCulture)}{pathAndQuery}";

        var apiKey = _options.CurrentValue.ResolvedIngestApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return baseUrl;
        }

        var separator = pathAndQuery.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return baseUrl + separator + "apiKey=" + Uri.EscapeDataString(apiKey);
    }
}
