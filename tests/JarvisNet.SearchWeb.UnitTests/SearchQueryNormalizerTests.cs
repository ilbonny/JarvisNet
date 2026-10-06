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
}
