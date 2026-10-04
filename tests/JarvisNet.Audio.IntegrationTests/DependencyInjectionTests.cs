using JarvisNet.Audio.Abstractions;
using JarvisNet.Audio.DependencyInjection;
using JarvisNet.Audio.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JarvisNet.Audio.IntegrationTests;

[TestFixture]
public sealed class DependencyInjectionTests
{
    [Test]
    public async Task AddJarvisNetAudio_RegistersCoreServices()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            Assert.Ignore("Supported only on Windows and Linux.");
        }

        var encoder = CreateTempModelFile("encoder.onnx");
        var decoder = CreateTempModelFile("decoder.onnx");
        var joiner = CreateTempModelFile("joiner.onnx");
        var tokens = CreateTempModelFile("tokens.txt");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AudioOptions.SectionName}:SampleRate"] = "16000",
                [$"{AudioOptions.SectionName}:PiperModelPath"] = "models/test.onnx",
                [$"{AudioOptions.SectionName}:SherpaEncoderPath"] = encoder,
                [$"{AudioOptions.SectionName}:SherpaDecoderPath"] = decoder,
                [$"{AudioOptions.SectionName}:SherpaJoinerPath"] = joiner,
                [$"{AudioOptions.SectionName}:SherpaTokensPath"] = tokens,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJarvisNetAudio(configuration);

        await using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetService<IAudioInputService>(), Is.Not.Null);
        Assert.That(provider.GetService<ISpeechToTextService>(), Is.Not.Null);
        Assert.That(provider.GetService<ITextToSpeechService>(), Is.Not.Null);
    }

    [Test]
    public void Configuration_BindsAudioOptionsSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AudioOptions.SectionName}:SampleRate"] = "16000",
                [$"{AudioOptions.SectionName}:InputMode"] = "Vad",
                [$"{AudioOptions.SectionName}:PiperModelPath"] = "models/it.onnx",
            })
            .Build();

        var options = new AudioOptions();
        configuration.GetSection(AudioOptions.SectionName).Bind(options);

        Assert.That(options.SampleRate, Is.EqualTo(16000));
        Assert.That(options.InputMode, Is.EqualTo(AudioInputMode.Vad));
        Assert.That(options.PiperModelPath, Is.EqualTo("models/it.onnx"));
    }

    private static string CreateTempModelFile(string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-{fileName}");
        File.WriteAllText(path, "test");
        return path;
    }
}
