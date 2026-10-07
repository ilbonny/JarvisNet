using JarvisNet.SearchWeb;

namespace JarvisNet.SearchWeb.UnitTests;

[TestFixture]
public sealed class BrowserResponseGuardTests
{
    [Test]
    public void ShouldRetry_WhenBookingAnswerWithoutTools_Italian()
    {
        var user = "cerca un hotel su booking.com a Los Cristianos";
        var assistant =
            "Il sito è aperto. Ecco i risultati:\n- **Albergo 1**: [Dettagli]";

        Assert.That(
            BrowserResponseGuard.ShouldRetryWithoutTools(user, assistant, toolsInvoked: 0),
            Is.True);
    }

    [Test]
    public void ShouldRetry_WhenBookingAnswerWithoutTools_English()
    {
        var user = "open booking.com and find hotels in Madrid";
        var assistant =
            "The browser is now open. Here are the results:\n- **Hotel 1**: [Details] — $120";

        Assert.That(
            BrowserResponseGuard.ShouldRetryWithoutTools(user, assistant, toolsInvoked: 0),
            Is.True);
    }

    [Test]
    public void ShouldNotRetry_WhenToolsWereUsed()
    {
        var user = "open booking.com";
        var assistant = "Here are the results:\n- **Hotel 1**: [Details]";

        Assert.That(
            BrowserResponseGuard.ShouldRetryWithoutTools(user, assistant, toolsInvoked: 2),
            Is.False);
    }

    [Test]
    public void ShouldNotRetry_ForUnrelatedSmallTalk()
    {
        Assert.That(
            BrowserResponseGuard.ShouldRetryWithoutTools("how are you?", "Fine, thanks.", toolsInvoked: 0),
            Is.False);
    }
}
