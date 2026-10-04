using JarvisNet.Audio;
using JarvisNet.Audio.Abstractions;
using JarvisNet.Audio.DependencyInjection;
using JarvisNet.Audio.Events;
using JarvisNet.Audio.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PiperSharp;

if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
{
    Console.WriteLine("JarvisNet.Audio.TestApp supporta solo Windows e Linux.");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJarvisNetSharedAudioConfiguration(builder.Environment.ContentRootPath);
builder.Services.AddJarvisNetAudio(builder.Configuration);

using var host = builder.Build();

var options = host.Services.GetRequiredService<IOptions<AudioOptions>>().Value;
await EnsurePiperOnWindowsAsync(options).ConfigureAwait(false);

var audioInput = host.Services.GetRequiredService<IAudioInputService>();
var speechToText = host.Services.GetRequiredService<ISpeechToTextService>();
var textToSpeech = host.Services.GetRequiredService<ITextToSpeechService>();

Console.WriteLine("=== JarvisNet.Audio TestApp ===");
var voiceKey = string.IsNullOrWhiteSpace(options.PiperVoiceKey)
    ? $"(path) {options.PiperModelPath}"
    : options.PiperVoiceKey;
Console.WriteLine($"Voce TTS: {voiceKey}");
Console.WriteLine("Dispositivi di input:");
foreach (var device in await audioInput.GetInputDevicesAsync())
{
    Console.WriteLine($"  [{device.DeviceNumber}] {device.Name}");
}

Console.WriteLine("Dispositivi di output:");
foreach (var device in AudioDevices.GetOutputDevices())
{
    Console.WriteLine($"  [{device.DeviceNumber}] {device.Name}");
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

if (options.InputMode == AudioInputMode.PushToTalk)
{
    Console.WriteLine("Modalità Push-To-Talk: tieni premuto SPAZIO mentre parli.");
    _ = Task.Run(() => MonitorPushToTalk(audioInput, cts.Token), cts.Token);
}

speechToText.SpeechRecognized += async (_, e) =>
{
    if (e.IsFinal)
    {
        Console.WriteLine();
        Console.WriteLine($"[FINAL] {e.Text}");
        try
        {
            await textToSpeech.SpeakAsync(
                    $"Ho sentito la tua voce. La trascrizione è: {e.Text}",
                    cts.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TTS error: {ex.Message}");
            Console.WriteLine(ex);
        }
    }
    else if (!string.IsNullOrWhiteSpace(e.Text))
    {
        Console.Write($"\r[partial] {e.Text}   ");
    }
};

audioInput.AudioFrameAvailable += (_, e) => speechToText.ProcessAudio(e.Samples, e.SampleRate);

await speechToText.StartSessionAsync(cts.Token).ConfigureAwait(false);
await audioInput.StartAsync(cts.Token).ConfigureAwait(false);

Console.WriteLine("Ascolto attivo. Premi INVIO per terminare.");
try
{
    await Task.Run(() => Console.ReadLine(), cts.Token).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    // ignored
}

await audioInput.StopAsync().ConfigureAwait(false);
await speechToText.StopSessionAsync().ConfigureAwait(false);

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
