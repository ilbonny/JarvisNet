using JarvisNet.Plugins.Sdk.Abstractions;

namespace JarvisNet.Plugins.Sdk.Services;

public static class JarvisPluginTurnCoordinator
{
    public static async Task<string?> TryEnhanceTurnAsync(
        IEnumerable<IJarvisPluginTurnHandler> handlers,
        JarvisPluginTurnContext context,
        CancellationToken cancellationToken)
    {
        foreach (var handler in handlers.OrderBy(static handler => handler.Order))
        {
            var outcome = await handler
                .TryEnhanceTurnAsync(context, cancellationToken)
                .ConfigureAwait(false);

            if (outcome is { IsHandled: true })
            {
                return outcome.Response;
            }
        }

        return null;
    }
}
