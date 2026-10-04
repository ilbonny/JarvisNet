using JarvisNet.Engine.Abstractions;
using JarvisNet.Engine.Options;
using JarvisNet.Engine.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace JarvisNet.Engine.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJarvisNetEngine(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));

        services.AddHttpClient(OllamaHttpClientNames.TagsApi, (sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

#pragma warning disable SKEXP0070
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
            var builder = Kernel.CreateBuilder();
            builder.AddOllamaChatCompletion(
                modelId: options.ModelName,
                endpoint: new Uri(options.Endpoint));
            return builder.Build();
        });

        services.AddSingleton<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>(sp =>
            sp.GetRequiredService<Kernel>().GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>());
#pragma warning restore SKEXP0070

        services.AddSingleton<OllamaBrainService>();
        services.AddSingleton<JarvisNetOrchestrator>();
        services.AddSingleton<OllamaModelVerifier>();
        services.AddSingleton<IJarvisNetEngine, JarvisNetEngine>();

        return services;
    }
}
