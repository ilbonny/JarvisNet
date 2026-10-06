using JarvisNet.Plugins.Sdk;
using JarvisNet.Plugins.Sdk.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace JarvisNet.Engine.Services;

/// <summary>Conteggio tool invocati nel turno (plugin Jarvis + MCP).</summary>
public sealed class JarvisTurnScope : IJarvisTurnScope, IFunctionInvocationFilter
{
    private readonly ILogger<JarvisTurnScope> _logger;
    private readonly object _lock = new();

    public JarvisTurnScope(ILogger<JarvisTurnScope> logger)
    {
        _logger = logger;
    }

    public int ToolsInvokedThisTurn { get; private set; }

    public void BeginUserTurn()
    {
        lock (_lock)
        {
            ToolsInvokedThisTurn = 0;
        }
    }

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        if (IsSearchWebInternetSearch(context))
        {
            var displayName = $"{SearchWebPluginMetadata.PluginName}.{SearchWebPluginMetadata.SearchInternetFunction}";
            var query = TryGetSearchQuery(context);
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogInformation("Tool ricerca internet avviato: {ToolName}", displayName);
            }
            else
            {
                _logger.LogInformation(
                    "Tool ricerca internet avviato: {ToolName} (query: {Query})",
                    displayName,
                    query);
            }
        }

        lock (_lock)
        {
            ToolsInvokedThisTurn++;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool IsSearchWebInternetSearch(FunctionInvocationContext context)
    {
        if (!string.Equals(
                context.Function.Name,
                SearchWebPluginMetadata.SearchInternetFunction,
                StringComparison.Ordinal))
        {
            return false;
        }

        var pluginName = context.Function.PluginName;
        return string.IsNullOrEmpty(pluginName)
            || string.Equals(pluginName, SearchWebPluginMetadata.PluginName, StringComparison.Ordinal);
    }

    private static string? TryGetSearchQuery(FunctionInvocationContext context)
    {
        if (context.Arguments.TryGetValue("query", out var value))
        {
            return value?.ToString();
        }

        return null;
    }
}
