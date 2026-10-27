using JarvisNet.Audio.Abstractions;
using JarvisNet.Audio.DependencyInjection;
using JarvisNet.Engine.Abstractions;
using JarvisNet.Engine.DependencyInjection;
using JarvisNet.Engine.Services;
using JarvisNet.Telegram.DependencyInjection;
using JarvisNet.UI.Hubs;
using JarvisNet.UI.Models;
using JarvisNet.UI.Services;

if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("JarvisNet.UI supporta solo Windows e Linux.");
    return 1;
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJarvisNetSharedAudioConfiguration(builder.Environment.ContentRootPath)
    .AddJarvisNetSharedEngineConfiguration(builder.Environment.ContentRootPath)
    .AddJarvisNetSharedTelegramConfiguration(builder.Environment.ContentRootPath);

builder.Services.AddJarvisNetAudio(builder.Configuration);
builder.Services.AddJarvisNetEngine(builder.Configuration);
builder.Services.AddJarvisNetTelegram(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddHostedService<JarvisUiEventBridge>();
builder.Services.AddSingleton<SystemStatusProvider>();
builder.Services.AddSingleton<IAudioLanguageService, AudioLanguageService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<JarvisHub>("/hub");

app.MapPost("/api/start", async (IJarvisNetEngine engine, CancellationToken cancellationToken) =>
{
    await engine.StartVoiceSessionAsync(cancellationToken).ConfigureAwait(false);
    return Results.Ok(new { status = "listening" });
});

app.MapPost("/api/stop", async (IJarvisNetEngine engine, CancellationToken cancellationToken) =>
{
    await engine.StopVoiceSessionAsync(cancellationToken).ConfigureAwait(false);
    return Results.Ok(new { status = "idle" });
});

app.MapGet("/api/system-status", (SystemStatusProvider telemetry) =>
    Results.Ok(telemetry.GetSnapshot()));

app.MapGet("/api/audio/settings", (IAudioLanguageService audio) =>
{
    var snapshot = audio.GetSettingsSnapshot();
    return Results.Ok(new
    {
        currentLanguage = snapshot.CurrentLanguageId,
        currentVoice = snapshot.CurrentVoiceKey,
        languages = snapshot.Languages.Select(l => new
        {
            id = l.Id,
            label = l.Label,
            culture = l.CultureName,
            installed = l.IsInstalled,
        }),
        voices = snapshot.Voices.Select(v => new
        {
            key = v.Key,
            name = v.SpeakerName,
            quality = v.Quality,
            gender = v.Gender,
            locale = v.Locale,
            installed = v.IsInstalled,
        }),
    });
});

app.MapGet("/api/tts/voices", (IAudioLanguageService audio) =>
{
    var snapshot = audio.GetSettingsSnapshot();
    return Results.Ok(new { current = snapshot.CurrentVoiceKey, voices = snapshot.Voices });
});

app.MapPut("/api/audio/language", async (AudioLanguageRequest request, IAudioLanguageService audio, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.LanguageId))
    {
        return Results.BadRequest(new { error = "LanguageId is required." });
    }

    try
    {
        await audio.SetLanguageAsync(request.LanguageId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { languageId = request.LanguageId });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPut("/api/tts/voice", async (TtsVoiceRequest request, IPiperVoiceService voices, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.VoiceKey))
    {
        return Results.BadRequest(new { error = "VoiceKey is required." });
    }

    try
    {
        await voices.SetVoiceAsync(request.VoiceKey, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { voiceKey = request.VoiceKey });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/modules", (JarvisModuleCatalogService catalog) =>
{
    var modules = catalog.GetModules()
        .Select(m => new
        {
            id = m.Id,
            kind = m.Kind,
            toolCount = m.ToolCount,
            description = m.Description,
        });
    return Results.Ok(modules);
});

app.MapFallbackToFile("index.html");

await app.RunAsync().ConfigureAwait(false);
return 0;
