using System.Globalization;
using System.Net;
using System.Text.Json;
using JarvisNet.SearchWeb;
using JarvisNet.SearchWeb.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JarvisNet.SearchWeb.UnitTests;

[TestFixture]
public sealed class BraveWebSearchServiceTests
{
    [Test]
    public void FormatResults_IncludesTitleUrlAndDescription()
    {
        var items = new List<BraveWebResultItem>
        {
            new()
            {
                Title = "Sensori parcheggio",
                Url = "https://example.org/parking",
                Description = "Guida ai sensori posteriori.",
            },
        };

        var labels = SearchResultLabelsForCulture.Get(CultureInfo.GetCultureInfo("it-IT"));
        var text = BraveWebSearchService.FormatResults("sensori auto", items, 5, labels);

        Assert.That(text, Does.Contain("Brave Search"));
        Assert.That(text, Does.Contain("Sensori parcheggio"));
        Assert.That(text, Does.Contain("https://example.org/parking"));
        Assert.That(text, Does.Contain("Guida ai sensori"));
    }

    [Test]
    public async Task SearchAsync_WithoutApiKey_ReturnsMissingKeyMessage()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = BraveWebSearchServiceRegistration.ConfigureClient(new HttpClient(handler));
        var options = Options.Create(new SearchWebOptions { ApiKey = string.Empty });
        var monitor = new TestOptionsMonitor(options);
        var service = new BraveWebSearchService(
            client,
            NullLogger<BraveWebSearchService>.Instance,
            monitor,
            CultureInfo.GetCultureInfo("it-IT"));

        var outcome = await service.SearchAsync("meteo roma", 5, CancellationToken.None);

        Assert.That(outcome.IsMissingApiKey, Is.True);
        Assert.That(outcome.Message, Does.Contain("BRAVE_SEARCH_API_KEY"));
    }

    [Test]
    public async Task SearchAsync_ParsesWebResults()
    {
        var payload = new BraveWebSearchResponse
        {
            Web = new BraveWebResults
            {
                Results =
                [
                    new BraveWebResultItem
                    {
                        Title = "Roma meteo",
                        Url = "https://example.org/meteo",
                        Description = "Previsioni.",
                    },
                ],
            },
        };

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload)),
        });
        var client = BraveWebSearchServiceRegistration.ConfigureClient(new HttpClient(handler));
        var options = Options.Create(new SearchWebOptions { ApiKey = "test-token" });
        var monitor = new TestOptionsMonitor(options);
        var service = new BraveWebSearchService(
            client,
            NullLogger<BraveWebSearchService>.Instance,
            monitor,
            CultureInfo.GetCultureInfo("it-IT"));

        var outcome = await service.SearchAsync("meteo roma", 5, CancellationToken.None);

        Assert.That(outcome.HasWebResults, Is.True);
        Assert.That(outcome.Message, Does.Contain("Roma meteo"));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class TestOptionsMonitor(IOptions<SearchWebOptions> options) : IOptionsMonitor<SearchWebOptions>
    {
        public SearchWebOptions CurrentValue => options.Value;

        public SearchWebOptions Get(string? name) => options.Value;

        public IDisposable? OnChange(Action<SearchWebOptions, string?> listener) => null;
    }
}
