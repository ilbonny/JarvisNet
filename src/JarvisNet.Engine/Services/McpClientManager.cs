using System.Text.Json;
using System.Text.RegularExpressions;
using JarvisNet.Engine.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace JarvisNet.Engine.Services;

public sealed partial class McpClientManager : IHostedService, IAsyncDisposable
{
    private readonly Kernel _kernel;
    private readonly McpOptions _options;
    private readonly ILogger<McpClientManager> _logger;
    private readonly List<McpClient> _clients = [];
    private readonly Lock _clientLock = new();

    public McpClientManager(
        Kernel kernel,
        IOptions<McpOptions> options,
        ILogger<McpClientManager> logger)
    {
        _kernel = kernel;
        _options = options.Value;
        _logger = logger;
    }

    public event EventHandler<string>? McpToolExecutionStarted;

    public event EventHandler? McpToolExecutionCompleted;

    public Task StartAsync(CancellationToken cancellationToken) =>
        ConnectAndRegisterToolsAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        List<McpClient> clients;
        lock (_clientLock)
        {
            clients = [.. _clients];
            _clients.Clear();
        }

        foreach (var client in clients)
        {
            try
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Errore durante la chiusura di un client MCP.");
            }
        }
    }

    private async Task ConnectAndRegisterToolsAsync(CancellationToken cancellationToken)
    {
        if (_options.Servers.Count == 0)
        {
            _logger.LogInformation("Nessun server MCP configurato.");
            return;
        }

        foreach (var (serverKey, config) in _options.Servers)
        {
            if (!config.Enabled)
            {
                _logger.LogInformation("Server MCP {ServerKey} disabilitato.", serverKey);
                continue;
            }

            if (string.IsNullOrWhiteSpace(config.Command))
            {
                _logger.LogWarning("Server MCP {ServerKey}: Command non specificato.", serverKey);
                continue;
            }

            try
            {
                await ConnectServerAsync(serverKey, config, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Impossibile avviare il server MCP {ServerKey}.", serverKey);
            }
        }
    }

    private async Task ConnectServerAsync(
        string serverKey,
        McpServerConfig config,
        CancellationToken cancellationToken)
    {
        var transportOptions = new StdioClientTransportOptions
        {
            Name = serverKey,
            Command = config.Command,
            Arguments = config.Args,
            EnvironmentVariables = BuildEnvironmentVariables(config.Env),
        };

        var transport = new StdioClientTransport(transportOptions);
        var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        lock (_clientLock)
        {
            _clients.Add(client);
        }

        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var pluginName = SanitizePluginName(serverKey);
        var functions = new List<KernelFunction>();

        foreach (var tool in tools)
        {
            var function = CreateKernelFunction(client, pluginName, tool);
            functions.Add(function);
        }

        if (functions.Count == 0)
        {
            _logger.LogInformation("Server MCP {ServerKey}: nessun tool esposto.", serverKey);
            return;
        }

        _kernel.ImportPluginFromFunctions(pluginName, functions);
        _logger.LogInformation(
            "Server MCP {ServerKey}: registrati {ToolCount} tool nel plugin {PluginName}.",
            serverKey,
            functions.Count,
            pluginName);
    }

    private KernelFunction CreateKernelFunction(McpClient client, string pluginName, McpClientTool tool)
    {
        var toolName = tool.Name;
        var description = tool.Description ?? toolName;
        var parameters = BuildParameterMetadata(tool);

        async Task<string> InvokeAsync(KernelArguments arguments, CancellationToken cancellationToken)
        {
            var displayName = $"{pluginName}.{toolName}";
            McpToolExecutionStarted?.Invoke(this, displayName);
            try
            {
                var args = ToArgumentDictionary(arguments);
                var result = await client
                    .CallToolAsync(toolName, args, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return ExtractToolResultText(result);
            }
            finally
            {
                McpToolExecutionCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        return KernelFunctionFactory.CreateFromMethod(
            InvokeAsync,
            functionName: SanitizeFunctionName(toolName),
            description: description,
            parameters: parameters,
            returnParameter: new KernelReturnParameterMetadata { ParameterType = typeof(string) });
    }

    private static List<KernelParameterMetadata> BuildParameterMetadata(McpClientTool tool)
    {
        var parameters = new List<KernelParameterMetadata>();
        var schema = tool.ProtocolTool.InputSchema;
        if (schema.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object)
        {
            return parameters;
        }

        var required = new HashSet<string>(StringComparer.Ordinal);
        if (schema.TryGetProperty("required", out var requiredElement)
            && requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in requiredElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    required.Add(item.GetString() ?? string.Empty);
                }
            }
        }

        foreach (var property in properties.EnumerateObject())
        {
            var paramSchema = property.Value;
            var description = paramSchema.TryGetProperty("description", out var descEl)
                && descEl.ValueKind == JsonValueKind.String
                ? descEl.GetString()
                : null;

            parameters.Add(new KernelParameterMetadata(property.Name)
            {
                Description = description,
                ParameterType = MapJsonSchemaType(paramSchema),
                IsRequired = required.Contains(property.Name),
            });
        }

        return parameters;
    }

    private static Type MapJsonSchemaType(JsonElement paramSchema)
    {
        if (paramSchema.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
        {
            return MapJsonSchemaTypeString(typeEl.GetString());
        }

        return typeof(string);
    }

    private static Type MapJsonSchemaTypeString(string? type) =>
        type switch
        {
            "integer" => typeof(long),
            "number" => typeof(double),
            "boolean" => typeof(bool),
            "object" => typeof(string),
            "array" => typeof(string),
            _ => typeof(string),
        };

    private static Dictionary<string, object?> ToArgumentDictionary(KernelArguments arguments)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in arguments)
        {
            if (value is null)
            {
                dict[key] = null;
                continue;
            }

            if (value is JsonElement jsonElement)
            {
                dict[key] = JsonElementToObject(jsonElement);
                continue;
            }

            dict[key] = value;
        }

        return dict;
    }

    private static object? JsonElementToObject(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.GetRawText(),
            JsonValueKind.Object => element.GetRawText(),
            _ => element.GetRawText(),
        };

    private static string ExtractToolResultText(CallToolResult result)
    {
        if (result.Content is null || result.Content.Count == 0)
        {
            return string.Empty;
        }

        var textParts = result.Content
            .OfType<TextContentBlock>()
            .Select(block => block.Text)
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList();

        if (textParts.Count == 1)
        {
            return textParts[0]!;
        }

        if (textParts.Count > 1)
        {
            return JsonSerializer.Serialize(textParts);
        }

        return JsonSerializer.Serialize(result.Content);
    }

    private static Dictionary<string, string?> BuildEnvironmentVariables(Dictionary<string, string> env)
    {
        if (env.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        var merged = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in env)
        {
            merged[key] = value;
        }

        return merged;
    }

    private static string SanitizePluginName(string serverKey)
    {
        var sanitized = InvalidPluginNameChars().Replace(serverKey, "_").Trim('_');
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "McpServer";
        }

        if (!char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized = "Mcp_" + sanitized;
        }

        return sanitized;
    }

    private static string SanitizeFunctionName(string toolName)
    {
        var sanitized = InvalidFunctionNameChars().Replace(toolName, "_").Trim('_');
        return string.IsNullOrEmpty(sanitized) ? "tool" : sanitized;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9_]", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidPluginNameChars();

    [GeneratedRegex(@"[^a-zA-Z0-9_]", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidFunctionNameChars();
}
