using JarvisNet.Engine.Abstractions;
using JarvisNet.Engine.Options;
using JarvisNet.Engine.Services;
using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.DependencyInjection;
using JarvisNet.Plugins.Sdk.Infrastructure;
using JarvisNet.Plugins.Sdk.Options;
using JarvisNet.Plugins.Sdk.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace JarvisNet.Engine.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJarvisNetEngine(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.AddOptions<McpOptions>()
            .Configure(options =>
            {
                options.Servers = configuration.GetSection(McpOptions.SectionName)
                        .Get<Dictionary<string, McpServerConfig>>()
                    ?? new Dictionary<string, McpServerConfig>(StringComparer.OrdinalIgnoreCase);
            });

        services.AddJarvisPlugins(configuration);
        services.AddSingleton<JarvisTurnScope>();
        services.AddSingleton<IJarvisTurnScope>(sp => sp.GetRequiredService<JarvisTurnScope>());

        services.AddHttpClient(OllamaHttpClientNames.TagsApi, (sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
            var turnScope = sp.GetRequiredService<JarvisTurnScope>();
            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton<IFunctionInvocationFilter>(turnScope);
            // Use Ollama's OpenAI-compatible endpoint (/v1/) so that SK's OpenAI connector
            // properly serializes tools and receives structured tool_calls in the response.
            var ollamaOpenAiEndpoint = new Uri(options.Endpoint.TrimEnd('/') + "/v1/");
            builder.AddOpenAIChatCompletion(
                modelId: options.ModelName,
                endpoint: ollamaOpenAiEndpoint,
                apiKey: "ollama");
            var kernel = builder.Build();

            var pluginOptions = sp.GetRequiredService<IOptions<PluginOptions>>().Value;
            var pluginsFolder = PluginFolderResolver.Resolve(pluginOptions);
            sp.GetRequiredService<PluginLoader>().LoadAndRegisterPlugins(kernel, pluginsFolder);

            return kernel;
        });

        services.AddSingleton<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>(sp =>
            sp.GetRequiredService<Kernel>().GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>());

        services.AddSingleton<McpClientManager>();
        services.AddHostedService(sp => sp.GetRequiredService<McpClientManager>());

        services.AddSingleton<OllamaBrainService>();
        services.AddSingleton<JarvisNetOrchestrator>();
        services.AddSingleton<OllamaModelVerifier>();
        services.AddSingleton<IJarvisNetEngine, JarvisNetEngine>();

        return services;
    }
}
