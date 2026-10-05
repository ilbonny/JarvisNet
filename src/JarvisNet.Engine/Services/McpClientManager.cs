using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using JarvisNet.Engine.Infrastructure;
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
    private readonly Lock _turnGuardLock = new();
    private readonly Dictionary<string, int> _toolCallsThisTurn = new(StringComparer.Ordinal);

    /// <summary>Tools not registered (Chrome DevTools tab loops).</summary>
    private static readonly HashSet<string> SuppressedToolNames =
        new(StringComparer.OrdinalIgnoreCase) { "select_page", "list_pages" };

    /// <summary>Only these tools use duplicate-call blocking (Playwright tools are excluded).</summary>
    private static readonly HashSet<string> StrictLoopGuardToolNames =
        new(StringComparer.OrdinalIgnoreCase) { "select_page", "list_pages" };

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

    /// <summary>Reset per-user-message tool counters (call before each LLM turn).</summary>
    public void BeginUserTurn()
    {
        lock (_turnGuardLock)
        {
            _toolCallsThisTurn.Clear();
        }
    }

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
                if (string.Equals(serverKey, "Playwright", StringComparison.OrdinalIgnoreCase))
                {
                    await EnsurePlaywrightBrowserAsync(config, cancellationToken).ConfigureAwait(false);
                }

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
        var environment = BuildEnvironmentVariables(serverKey, config.Env);
        var arguments = BuildServerArguments(serverKey, config.Args);
        var command = NodeToolchainResolver.ResolveExecutable(config.Command);
        var transportOptions = new StdioClientTransportOptions
        {
            Name = serverKey,
            Command = command,
            Arguments = arguments,
            EnvironmentVariables = environment,
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
            if (SuppressedToolNames.Contains(tool.Name))
            {
                _logger.LogInformation(
                    "Server MCP {ServerKey}: tool {ToolName} non esposto al modello (anti-loop).",
                    serverKey,
                    tool.Name);
                continue;
            }

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
            var args = ToArgumentDictionary(arguments);
            if (TryGetLoopGuardResponse(toolName, args, out var guardMessage))
            {
                _logger.LogWarning(
                    "Server MCP {ServerKey}: bloccata ripetizione tool {ToolName}.",
                    pluginName,
                    toolName);
                return guardMessage;
            }

            McpToolExecutionStarted?.Invoke(this, displayName);
            try
            {
                var result = await client
                    .CallToolAsync(toolName, args, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                var text = ExtractToolResultText(result);
                if (result.IsError == true
                    || text.Contains("install", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Tool MCP {ToolName} risposta: {Preview}",
                        displayName,
                        text.Length > 500 ? text[..500] + "…" : text);
                }

                return text;
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

    private bool TryGetLoopGuardResponse(
        string toolName,
        Dictionary<string, object?> args,
        out string message)
    {
        if (!StrictLoopGuardToolNames.Contains(toolName))
        {
            message = string.Empty;
            return false;
        }

        var signature = toolName + ":" + JsonSerializer.Serialize(args);
        lock (_turnGuardLock)
        {
            _toolCallsThisTurn.TryGetValue(signature, out var count);
            count++;
            _toolCallsThisTurn[signature] = count;

            if (count <= 1)
            {
                message = string.Empty;
                return false;
            }

            message =
                "Tool già eseguito con gli stessi parametri. Non richiamarlo di nuovo: "
                + "passa al passo successivo (es. evaluate_js o fill) oppure rispondi all'utente in italiano.";
            return true;
        }
    }

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

    private const int MaxToolResultCharacters = 12_000;

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

        string text;
        if (textParts.Count == 1)
        {
            text = textParts[0]!;
        }
        else if (textParts.Count > 1)
        {
            text = JsonSerializer.Serialize(textParts);
        }
        else
        {
            text = JsonSerializer.Serialize(result.Content);
        }

        if (text.Length <= MaxToolResultCharacters)
        {
            return text;
        }

        return text[..MaxToolResultCharacters]
               + "\n…[output troncato per limiti contesto LLM]";
    }

    private static string[] BuildServerArguments(string serverKey, string[] configuredArgs)
    {
        if (!string.Equals(serverKey, "Playwright", StringComparison.OrdinalIgnoreCase))
        {
            return configuredArgs;
        }

        var repoRoot = RepositoryRootLocator.Find(AppContext.BaseDirectory);
        var playwrightConfigPath = Path.Combine(repoRoot, "config", "playwright-mcp.json");
        if (!File.Exists(playwrightConfigPath))
        {
            return configuredArgs;
        }

        var args = new List<string>(configuredArgs)
        {
            "--config",
            playwrightConfigPath,
        };
        return args.ToArray();
    }

    private static Dictionary<string, string?> BuildEnvironmentVariables(
        string serverKey,
        Dictionary<string, string> env)
    {
        var merged = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in env)
        {
            merged[key] = value;
        }

        if (string.Equals(serverKey, "Playwright", StringComparison.OrdinalIgnoreCase))
        {
            NodeToolchainResolver.PrependNodeToPath(merged);
            if (!merged.ContainsKey("PLAYWRIGHT_BROWSERS_PATH"))
            {
                merged["PLAYWRIGHT_BROWSERS_PATH"] = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ms-playwright");
            }
        }

        return merged;
    }

    private async Task EnsurePlaywrightBrowserAsync(McpServerConfig config, CancellationToken cancellationToken)
    {
        var environment = BuildEnvironmentVariables("Playwright", config.Env);
        var npx = NodeToolchainResolver.ResolveExecutable(config.Command);
        var psi = new ProcessStartInfo
        {
            FileName = npx,
            Arguments = "-y --package=playwright --package=@playwright/mcp playwright install chrome chromium",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var (key, value) in environment)
        {
            psi.Environment[key] = value ?? string.Empty;
        }

        _logger.LogInformation("Verifica browser Playwright (Chrome/Chromium) per MCP…");

        using var process = Process.Start(psi);
        if (process is null)
        {
            _logger.LogWarning("Impossibile avviare playwright install.");
            return;
        }

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "playwright install exit {ExitCode}. stderr: {Stderr}",
                process.ExitCode,
                string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        }
        else
        {
            _logger.LogInformation("Browser Playwright pronti.");
        }
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
