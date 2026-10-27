using JarvisNet.Telegram.Options;
using JarvisNet.Telegram.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;

namespace JarvisNet.Telegram.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJarvisNetTelegram(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.BotToken))
            {
                return new TelegramBotClient("0:placeholder");
            }

            return new TelegramBotClient(options.BotToken);
        });

        services.AddSingleton<JarvisTelegramUpdateHandler>();
        services.AddHostedService<JarvisTelegramBotHostedService>();

        return services;
    }
}
