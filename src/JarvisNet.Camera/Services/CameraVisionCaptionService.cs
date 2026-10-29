using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JarvisNet.Camera.Options;
using JarvisNet.Engine.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JarvisNet.Camera.Services;

public sealed class CameraVisionCaptionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<CameraOptions> _camera;
    private readonly IOptionsMonitor<OllamaOptions> _ollama;
    private readonly ILogger<CameraVisionCaptionService> _logger;

    public CameraVisionCaptionService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<CameraOptions> camera,
        IOptionsMonitor<OllamaOptions> ollama,
        ILogger<CameraVisionCaptionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _camera = camera;
        _ollama = ollama;
        _logger = logger;
    }

    public async Task<string?> TryDescribeImageAsync(ReadOnlyMemory<byte> jpegBytes, CancellationToken cancellationToken)
    {
        var options = _camera.CurrentValue;
        if (!options.VisionAnalysisEnabled || jpegBytes.Length == 0)
        {
            return null;
        }

        var model = ResolveVisionModel(options);
        if (string.IsNullOrWhiteSpace(model))
        {
            _logger.LogWarning("Vision analysis enabled but no VisionModelName configured.");
            return null;
        }

        var client = _httpClientFactory.CreateClient(CameraOllamaHttpClient.Name);
        var endpoint = _ollama.CurrentValue.Endpoint.TrimEnd('/');

        var payload = new OllamaChatRequest
        {
            Model = model,
            Stream = false,
            KeepAlive = options.VisionKeepAliveSeconds,
            Options = new OllamaRunOptions
            {
                NumPredict = options.VisionMaxTokens,
                Temperature = 0.2f,
            },
            Messages =
            [
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = options.VisionPrompt,
                    Images = [Convert.ToBase64String(jpegBytes.Span)],
                },
            ],
        };

        try
        {
            using var response = await client
                .PostAsJsonAsync($"{endpoint}/api/chat", payload, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning(
                    "Ollama vision failed ({StatusCode}) model {Model}: {Body}",
                    (int)response.StatusCode,
                    model,
                    Truncate(body, 300));
                return null;
            }

            var chat = await response.Content
                .ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var text = NormalizeModelText(chat?.Message?.Content);
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning(
                    "Ollama vision model {Model} returned HTTP 200 but empty caption.",
                    model);
                return null;
            }

            _logger.LogInformation("Vision caption ({Model}): {Caption}", model, text);
            return text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Ollama vision request failed for model {Model}.", model);
            return null;
        }
    }

    private static string? ResolveVisionModel(CameraOptions camera)
    {
        if (!string.IsNullOrWhiteSpace(camera.VisionModelName))
        {
            return camera.VisionModelName.Trim();
        }

        return null;
    }

    private static string? NormalizeModelText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        var thinkClose = string.Concat("</", "think>");
        var thinkEnd = text.LastIndexOf(thinkClose, StringComparison.OrdinalIgnoreCase);
        if (thinkEnd >= 0)
        {
            text = text[(thinkEnd + thinkClose.Length)..].Trim();
        }

        text = text.Trim('"', '\'', ' ', '\r', '\n');
        var oneLine = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (oneLine.Contains("  ", StringComparison.Ordinal))
        {
            oneLine = oneLine.Replace("  ", " ", StringComparison.Ordinal);
        }

        return oneLine.Length <= 220 ? oneLine : oneLine[..217] + "...";
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("keep_alive")]
        public int KeepAlive { get; set; }

        [JsonPropertyName("options")]
        public OllamaRunOptions? Options { get; set; }

        [JsonPropertyName("messages")]
        public OllamaChatMessage[] Messages { get; set; } = [];
    }

    private sealed class OllamaRunOptions
    {
        [JsonPropertyName("num_predict")]
        public int NumPredict { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }
    }

    private sealed class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("images")]
        public string[]? Images { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaChatMessage? Message { get; set; }
    }
}
