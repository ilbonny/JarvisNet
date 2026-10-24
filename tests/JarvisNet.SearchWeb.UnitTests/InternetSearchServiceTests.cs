using System.Globalization;
using System.Net;
using JarvisNet.SearchWeb;
using Microsoft.Extensions.Logging.Abstractions;

namespace JarvisNet.SearchWeb.UnitTests;

[TestFixture]
public sealed class InternetSearchServiceTests
{
    [Test]
    public async Task SearchAsync_NewsQuery_UsesGoogleNewsNotDuckDuckGo()
    {
        var ddgHandler = new RecordingHttpMessageHandler(_ =>
            throw new InvalidOperationException("DuckDuckGo must not be called for news queries."));
        var ddgClient = DuckDuckGoSearchServiceRegistration.ConfigureClient(new HttpClient(ddgHandler));
        var duckDuckGo = new DuckDuckGoSearchService(
            ddgClient,
            NullLogger<DuckDuckGoSearchService>.Instance,
            CultureInfo.GetCultureInfo("it-IT"));

        const string rss =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel>
            <item><title>Primo titolo</title></item>
            </channel></rss>
            """;
        var newsHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(rss),
        });
        var newsClient = GoogleNewsRssSearchServiceRegistration.ConfigureClient(new HttpClient(newsHandler));
        var googleNews = new GoogleNewsRssSearchService(
            newsClient,
            NullLogger<GoogleNewsRssSearchService>.Instance,
            CultureInfo.GetCultureInfo("it-IT"));

        var service = new InternetSearchService(duckDuckGo, CultureInfo.GetCultureInfo("it-IT"), googleNews);

        var result = await service.SearchAsync("notizie di oggi", 5, CancellationToken.None);

        Assert.That(result, Does.Contain("Primo titolo"));
        Assert.That(result, Does.Contain("Google News"));
        Assert.That(ddgHandler.CallCount, Is.EqualTo(0));
    }

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request));
        }
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
