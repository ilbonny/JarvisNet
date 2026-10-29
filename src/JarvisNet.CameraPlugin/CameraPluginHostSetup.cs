using JarvisNet.Camera.Abstractions;
using JarvisNet.Camera.Services;
using JarvisNet.Plugins.Sdk.Abstractions;
using JarvisNet.Plugins.Sdk.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JarvisNet.CameraPlugin;

public sealed class CameraPluginHostSetup : IJarvisPluginHostSetup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.AddJarvisPluginTurnHandler<CameraPluginTurnHandler>();
    }
}
