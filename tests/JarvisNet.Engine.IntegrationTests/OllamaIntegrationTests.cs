using JarvisNet.Engine.DependencyInjection;
using JarvisNet.Engine.Options;
using JarvisNet.Engine.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JarvisNet.Engine.IntegrationTests;

[TestFixture]
public sealed class OllamaIntegrationTests
{
    [Test]
    public async Task Ollama_responds_in_italian()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJarvisNetEngine(configuration);

        await using var provider = services.BuildServiceProvider();

        var verifier = provider.GetRequiredService<OllamaModelVerifier>();
        if (!await verifier.IsModelAvailableAsync().ConfigureAwait(false))
        {
            Assert.Ignore("Ollama non disponibile o modello jarvis:1b-new assente.");
        }

        var brain = provider.GetRequiredService<OllamaBrainService>();
        var reply = await brain
            .SendAsync("Rispondi solo con la parola: ciao", CancellationToken.None)
            .ConfigureAwait(false);

        Assert.That(reply, Does.Contain("ciao").IgnoreCase);
    }

    [Test]
    public async Task AddJarvisNetEngine_RegistersCoreServices()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJarvisNetEngine(configuration);

        await using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetService<OllamaBrainService>(), Is.Not.Null);
        Assert.That(provider.GetService<OllamaModelVerifier>(), Is.Not.Null);
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OllamaOptions.SectionName}:Endpoint"] = "http://localhost:11434",
                [$"{OllamaOptions.SectionName}:ModelName"] = "jarvis:1b-new",
                [$"{OllamaOptions.SectionName}:SystemPrompt"] =
                    "Sei JarvisNet. Rispondi in italiano in modo breve.",
                [$"{OllamaOptions.SectionName}:Temperature"] = "0.1",
            })
            .Build();
    }
}
