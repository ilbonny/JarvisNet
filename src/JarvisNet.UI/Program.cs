using JarvisNet.Audio.DependencyInjection;
using JarvisNet.Engine.Abstractions;
using JarvisNet.Engine.DependencyInjection;
using JarvisNet.Engine.Services;
using JarvisNet.UI.Hubs;
using JarvisNet.UI.Services;

if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("JarvisNet.UI supporta solo Windows e Linux.");
    return 1;
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJarvisNetSharedAudioConfiguration(builder.Environment.ContentRootPath)
    .AddJarvisNetSharedEngineConfiguration(builder.Environment.ContentRootPath);

builder.Services.AddJarvisNetAudio(builder.Configuration);
builder.Services.AddJarvisNetEngine(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddHostedService<JarvisUiEventBridge>();

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
