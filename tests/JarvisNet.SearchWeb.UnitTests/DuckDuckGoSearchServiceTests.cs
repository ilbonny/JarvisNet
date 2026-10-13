using System.Globalization;
using System.Net;
using JarvisNet.SearchWeb;
using JarvisNet.SearchWeb.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace JarvisNet.SearchWeb.UnitTests;

[TestFixture]
public sealed class DuckDuckGoSearchServiceTests
{
    [Test]
    public void FormatAnswer_IncludesAbstractAndRelatedTopics()
    {
        var payload = new DuckDuckGoInstantAnswer
        {
            Heading = "JavaScript",
            AbstractText = "Linguaggio di programmazione.",
            AbstractSource = "Wikipedia",
            AbstractURL = "https://example.org/js",
            RelatedTopics =
            [
                new DuckDuckGoRelatedTopic { Text = "Node.js", FirstURL = "https://example.org/node" },
            ],
        };

        var labels = SearchResultLabelsForCulture.Get(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        var text = DuckDuckGoSearchService.FormatAnswer("javascript", payload, 5, labels);

        Assert.That(text, Does.Contain("JavaScript"));
        Assert.That(text, Does.Contain("Linguaggio di programmazione"));
        Assert.That(text, Does.Contain("Node.js"));
    }

    [Test]
    public async Task SearchAsync_EmptyBody_ReturnsGracefulMessage()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty),
        });
        var client = DuckDuckGoSearchServiceRegistration.ConfigureClient(new HttpClient(handler));
        var service = new DuckDuckGoSearchService(
            client,
            NullLogger<DuckDuckGoSearchService>.Instance,
            CultureInfo.GetCultureInfo("it-IT"));

        var result = await service.SearchAsync("meteo roma", 5, CancellationToken.None);

        Assert.That(result, Does.Contain("interpretabile").Or.Contain("interpret"));
        Assert.That(result, Does.Not.Contain("JsonException"));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
