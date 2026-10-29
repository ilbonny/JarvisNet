using JarvisNet.Camera.Options;
using JarvisNet.Camera.Services;
using JarvisNet.Engine.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JarvisNet.Camera.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJarvisNetCamera(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CameraOptions>(configuration.GetSection(CameraOptions.SectionName));

        services.AddHttpClient(CameraOllamaHttpClient.Name, (sp, client) =>
        {
            var ollama = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(90);
            if (!string.IsNullOrWhiteSpace(ollama.Endpoint))
            {
                client.BaseAddress = new Uri(ollama.Endpoint.TrimEnd('/') + "/");
            }
        });

        services.AddHttpClient(CameraDeviceHttpClient.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<CameraOptions>>().CurrentValue;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(15, options.MaxVideoSeconds + 10));
        });

        services.AddSingleton<CameraVisionCaptionService>();
        services.AddSingleton<CameraMotionAlertService>();
        services.AddSingleton<ConnectedDeviceRegistry>();
        services.AddSingleton<CameraMediaCache>();
        services.AddSingleton<ICameraTurnMediaBuffer, CameraTurnMediaBuffer>();
        services.AddSingleton<CameraDeviceHttpClient>();
        services.AddSingleton<CameraControlService>();
        services.AddSingleton<CameraMediaDeliveryService>();
        return services;
    }
}
