using JarvisNet.Engine.Options;
using Microsoft.Extensions.Configuration;

namespace JarvisNet.Engine.UnitTests;

[TestFixture]
public sealed class OllamaOptionsTests
{
    [Test]
    public void Configuration_BindsOllamaOptionsSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OllamaOptions.SectionName}:Endpoint"] = "http://127.0.0.1:11434",
                [$"{OllamaOptions.SectionName}:ModelName"] = "jarvis:1b-new",
                [$"{OllamaOptions.SectionName}:SystemPrompt"] = "Prompt di test",
                [$"{OllamaOptions.SectionName}:Temperature"] = "0.2",
            })
            .Build();

        var options = new OllamaOptions();
        configuration.GetSection(OllamaOptions.SectionName).Bind(options);

        Assert.That(options.Endpoint, Is.EqualTo("http://127.0.0.1:11434"));
        Assert.That(options.ModelName, Is.EqualTo("jarvis:1b-new"));
        Assert.That(options.SystemPrompt, Is.EqualTo("Prompt di test"));
        Assert.That(options.Temperature, Is.EqualTo(0.2f).Within(0.001f));
    }

    [Test]
    public void DefaultOptions_UseExpectedValues()
    {
        var options = new OllamaOptions();

        Assert.That(options.Endpoint, Is.EqualTo("http://localhost:11434"));
        Assert.That(options.ModelName, Is.EqualTo("jarvis:1b-new"));
        Assert.That(options.Temperature, Is.EqualTo(0.7f).Within(0.001f));
        Assert.That(options.SystemPrompt, Does.Contain("JarvisNet"));
    }
}
