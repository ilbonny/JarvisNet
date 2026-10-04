using JarvisNet.Audio;
using JarvisNet.Audio.Abstractions;
using JarvisNet.Audio.DependencyInjection;
using JarvisNet.Audio.Models;
using JarvisNet.Engine.Abstractions;
using JarvisNet.Engine.DependencyInjection;
using JarvisNet.Engine.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PiperSharp;

if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
{
    Console.WriteLine("JarvisNet.Engine.TestApp supporta solo Windows e Linux.");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJarvisNetSharedAudioConfiguration(builder.Environment.ContentRootPath)
    .AddJarvisNetSharedEngineConfiguration(builder.Environment.ContentRootPath);
builder.Services.AddJarvisNetAudio(builder.Configuration);
builder.Services.AddJarvisNetEngine(builder.Configuration);

using var host = builder.Build();

var audioOptions = host.Services.GetRequiredService<IOptions<AudioOptions>>().Value;
await EnsurePiperOnWindowsAsync(audioOptions).ConfigureAwait(false);

var modelVerifier = host.Services.GetRequiredService<OllamaModelVerifier>();
await modelVerifier.EnsureModelAvailableAsync().ConfigureAwait(false);

var engine = host.Services.GetRequiredService<IJarvisNetEngine>();
var audioInput = host.Services.GetRequiredService<IAudioInputService>();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

engine.UserTranscriptReceived += (_, text) => Console.WriteLine($"Tu: {text}");
engine.AssistantResponseReceived += (_, text) => Console.WriteLine($"JarvisNet: {text}");

Console.WriteLine("=== JarvisNet.Engine TestApp ===");
Console.WriteLine("In ascolto... (dì \"esci\" o premi INVIO per terminare)");

if (audioOptions.InputMode == AudioInputMode.PushToTalk)
{
    Console.WriteLine("Modalità Push-To-Talk: tieni premuto SPAZIO mentre parli.");
    _ = Task.Run(() => MonitorPushToTalk(audioInput, cts.Token), cts.Token);
}

var sessionTask = engine.StartVoiceSessionAsync(cts.Token);

try
{
    await Task.Run(() => Console.ReadLine(), cts.Token).ConfigureAwait(false);
    await cts.CancelAsync().ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    // ignored
}

try
{
    await sessionTask.ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    // ignored
}

await engine.StopVoiceSessionAsync(CancellationToken.None).ConfigureAwait(false);
return 0;

static async Task EnsurePiperOnWindowsAsync(AudioOptions options)
{
    if (!OperatingSystem.IsWindows())
    {
        return;
    }

    var executable = PiperPaths.ResolveExecutablePath(options);
    if (File.Exists(executable))
    {
        return;
    }

    Console.WriteLine("Piper non trovato. Download in corso...");
    var cwd = Directory.GetCurrentDirectory();
    await PiperDownloader.DownloadPiper().ExtractPiper(cwd).ConfigureAwait(false);
}

static void MonitorPushToTalk(IAudioInputService audioInput, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);
            audioInput.SetPushToTalkActive(key.Key == ConsoleKey.Spacebar);
        }

        Thread.Sleep(20);
    }
}
