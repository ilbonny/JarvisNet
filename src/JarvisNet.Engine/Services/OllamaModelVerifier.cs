using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JarvisNet.Engine.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JarvisNet.Engine.Services;

public sealed class OllamaModelVerifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaModelVerifier> _logger;

    public OllamaModelVerifier(
        IHttpClientFactory httpClientFactory,
        IOptions<OllamaOptions> options,
        ILogger<OllamaModelVerifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureModelAvailableAsync(CancellationToken cancellationToken = default)
    {
        var available = await IsModelAvailableAsync(cancellationToken).ConfigureAwait(false);
        if (available)
        {
            return;
        }

        var installed = await ListInstalledModelNamesAsync(cancellationToken).ConfigureAwait(false);
        var catalog = installed.Count > 0
            ? string.Join(", ", installed)
            : "(nessun modello in /api/tags)";

        throw new InvalidOperationException(
            $"Modello Ollama '{_options.ModelName}' non trovato su '{_options.Endpoint}'. " +
            $"Modelli installati: {catalog}. " +
            $"Installa il modello (es. ollama pull {_options.ModelName}).");
    }

    public async Task<bool> IsModelAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(OllamaHttpClientNames.TagsApi);
            using var response = await client.GetAsync("/api/tags", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Ollama tags API returned {StatusCode} from {Endpoint}.",
                    response.StatusCode,
                    _options.Endpoint);
                return false;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken)
                .ConfigureAwait(false);

            if (payload?.Models is null || payload.Models.Count == 0)
            {
                return false;
            }

            return payload.Models.Any(m => ModelNamesMatch(_options.ModelName, m.Name));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            _logger.LogDebug(ex, "Ollama non raggiungibile su {Endpoint}.", _options.Endpoint);
            return false;
        }
    }

    private async Task<IReadOnlyList<string>> ListInstalledModelNamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(OllamaHttpClientNames.TagsApi);
            using var response = await client.GetAsync("/api/tags", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var payload = await response.Content
                .ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken)
                .ConfigureAwait(false);

            return payload?.Models?.Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return [];
        }
    }

    public static bool ModelNamesMatch(string configured, string installed)
    {
        if (string.Equals(configured, installed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (installed.EndsWith(":latest", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(configured, installed[..^(":latest".Length)], StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals($"{configured}:latest", installed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private sealed class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelTag> Models { get; init; } = [];
    }

    private sealed class OllamaModelTag
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }
}

public static class OllamaHttpClientNames
{
    public const string TagsApi = "JarvisNet.Ollama.TagsApi";
}
