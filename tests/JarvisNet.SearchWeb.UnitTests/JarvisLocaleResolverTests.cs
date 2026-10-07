using JarvisNet.Plugins.Sdk.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace JarvisNet.SearchWeb.UnitTests;

[TestFixture]
public sealed class JarvisLocaleResolverTests
{
    [Test]
    public void ResolveCultureName_UsesExplicitLocale_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JarvisNet:Locale:CultureName"] = "fr-FR",
                ["JarvisNet:Audio:PiperVoiceKey"] = "it_IT-paola-medium",
            })
            .Build();

        var culture = JarvisLocaleResolver.ResolveCultureName(configuration);

        Assert.That(culture, Is.EqualTo("fr-FR"));
    }

    [Test]
    public void ResolveCultureName_DerivesFromPiperVoiceKey_WhenLocaleEmpty()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JarvisNet:Audio:PiperVoiceKey"] = "it_IT-paola-medium",
            })
            .Build();

        Assert.That(JarvisLocaleResolver.ResolveCultureName(configuration), Is.EqualTo("it-IT"));
    }

    [Test]
    public void ResolveCultureName_DerivesFromModelPath_WhenVoiceKeyMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JarvisNet:Audio:PiperModelPath"] = "tts/it_IT-paola-medium.onnx",
            })
            .Build();

        Assert.That(JarvisLocaleResolver.ResolveCultureName(configuration), Is.EqualTo("it-IT"));
    }
}
