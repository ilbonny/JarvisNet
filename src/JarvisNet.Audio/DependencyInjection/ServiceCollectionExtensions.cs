using System.Runtime.InteropServices;
using JarvisNet.Audio.Abstractions;
using JarvisNet.Audio.Internal;
using JarvisNet.Audio.Models;
using JarvisNet.Audio.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JarvisNet.Audio.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJarvisNetAudio(this IServiceCollection services, IConfiguration configuration)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            throw new PlatformNotSupportedException("JarvisNet.Audio is supported on Windows and Linux only (macOS is not supported in v1).");
        }

        services.Configure<AudioOptions>(configuration.GetSection(AudioOptions.SectionName));
        services.AddSingleton<IConfigureOptions<AudioOptions>>(sp =>
            new ConfigureOptions<AudioOptions>(options =>
            {
                var env = sp.GetService<IHostEnvironment>();
                var startPath = env?.ContentRootPath ?? AppContext.BaseDirectory;
                var repositoryRoot = RepositoryPaths.FindRepositoryRoot(startPath);

                if (string.IsNullOrWhiteSpace(options.ModelsRoot))
                {
                    options.ModelsRoot = RepositoryPaths.GetModelsDirectory(repositoryRoot);
                }

                if (string.IsNullOrWhiteSpace(options.PiperWorkingDirectory))
                {
                    options.PiperWorkingDirectory = RepositoryPaths.GetPiperDirectory(repositoryRoot);
                }
            }));
        services.AddSingleton<IValidateOptions<AudioOptions>, AudioOptionsValidator>();

        services.AddSingleton<IAudioInputService, NAudioInputService>();
        services.AddSingleton<ISpeechToTextService, SherpaOnnxSttService>();
        services.AddSingleton<ITextToSpeechService, PiperTtsService>();

        return services;
    }
}
