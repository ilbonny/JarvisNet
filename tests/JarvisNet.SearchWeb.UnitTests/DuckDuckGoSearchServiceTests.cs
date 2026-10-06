using JarvisNet.SearchWeb;
using JarvisNet.SearchWeb.Models;

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

        var text = DuckDuckGoSearchService.FormatAnswer("javascript", payload, 5);

        Assert.That(text, Does.Contain("JavaScript"));
        Assert.That(text, Does.Contain("Linguaggio di programmazione"));
        Assert.That(text, Does.Contain("Node.js"));
    }
}
