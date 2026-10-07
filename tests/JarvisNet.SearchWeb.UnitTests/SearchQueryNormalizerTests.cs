using JarvisNet.SearchWeb;

namespace JarvisNet.SearchWeb.UnitTests;

[TestFixture]
public sealed class SearchQueryNormalizerTests
{
    [Test]
    public void Normalize_StripsSearchLead_ForNewsRequest()
    {
        var normalized = SearchQueryNormalizer.Normalize(
            "Fai una ricerca su internet per sapere le notizie attuali");

        Assert.That(normalized, Does.Contain("notizie").IgnoreCase);
        Assert.That(normalized, Does.Not.Contain("internet").IgnoreCase);
    }

    [Test]
    public void PreferTopHeadlinesFeed_True_ForGeneralNews()
    {
        Assert.That(SearchQueryNormalizer.PreferTopHeadlinesFeed("notizie attuali"), Is.True);
    }

    [Test]
    public void Normalize_StripsSearchLead_EnglishNewsRequest()
    {
        var normalized = SearchQueryNormalizer.Normalize(
            "Please do a web search for the latest news today");

        Assert.That(normalized, Does.Contain("news").IgnoreCase);
        Assert.That(normalized, Does.Not.Contain("internet").IgnoreCase);
    }

    [Test]
    public void PreferTopHeadlinesFeed_True_ForEnglishHeadlinesOnly()
    {
        Assert.That(SearchQueryNormalizer.PreferTopHeadlinesFeed("today's news"), Is.True);
    }
}
