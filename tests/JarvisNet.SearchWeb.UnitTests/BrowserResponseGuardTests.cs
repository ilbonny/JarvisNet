using JarvisNet.SearchWeb;

namespace JarvisNet.SearchWeb.UnitTests;

[TestFixture]
public sealed class BrowserResponseGuardTests
{
    [Test]
    public void ShouldRetry_WhenBookingAnswerWithoutTools()
    {
        var user = "cerca un hotel su booking.com a Los Cristianos";
        var assistant =
            "Il sito è aperto. Ecco i risultati:\n- **Albergo 1**: [Dettagli]";

        Assert.That(
            BrowserResponseGuard.ShouldRetryWithoutTools(user, assistant, toolsInvoked: 0),
            Is.True);
    }

    [Test]
    public void ShouldNotRetry_WhenToolsWereUsed()
    {
        var user = "apri booking.com";
        var assistant = "Ecco i risultati:\n- **Albergo 1**: [Dettagli]";

        Assert.That(
            BrowserResponseGuard.ShouldRetryWithoutTools(user, assistant, toolsInvoked: 2),
            Is.False);
    }

    [Test]
    public void ShouldNotRetry_ForUnrelatedSmallTalk()
    {
        Assert.That(
            BrowserResponseGuard.ShouldRetryWithoutTools("come stai?", "Bene, grazie.", toolsInvoked: 0),
            Is.False);
    }
}
