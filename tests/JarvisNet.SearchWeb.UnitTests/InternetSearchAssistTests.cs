using JarvisNet.SearchWeb;

namespace JarvisNet.SearchWeb.UnitTests;

[TestFixture]
public sealed class InternetSearchAssistTests
{
    [Test]
    public void TryBuildSearchQuery_ExtractsTopic_FromInternetSearchPhrase()
    {
        var ok = InternetSearchAssist.TryBuildSearchQuery(
            "Allora fai una ricerca su internet per vedere quali sono le notizie di oggi",
            out var query);

        Assert.That(ok, Is.True);
        Assert.That(query, Does.Contain("notizie").IgnoreCase);
    }

    [Test]
    public void TryBuildSearchQuery_Fails_WhenOnlyGenericSearchRequest()
    {
        var ok = InternetSearchAssist.TryBuildSearchQuery("Ora fai una ricerca su internet", out _);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void IsInternetSearchRequest_True_ForCercaInInternet()
    {
        Assert.That(
            InternetSearchAssist.IsInternetSearchRequest("cerca in internet i cavi hdmi"),
            Is.True);
    }
}
