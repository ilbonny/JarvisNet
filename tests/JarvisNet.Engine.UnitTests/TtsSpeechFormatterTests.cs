namespace JarvisNet.Engine.UnitTests;

[TestFixture]
public sealed class TtsSpeechFormatterTests
{
    [Test]
    public void SanitizeForSpeech_RemovesBoldMarkdown()
    {
        var result = TtsSpeechFormatter.SanitizeForSpeech("Ciao **mondo**!");
        Assert.That(result, Is.EqualTo("Ciao mondo!"));
    }

    [Test]
    public void SanitizeForSpeech_RemovesCodeFence()
    {
        var result = TtsSpeechFormatter.SanitizeForSpeech("Esempio: ```var x = 1;``` fine.");
        Assert.That(result, Does.Not.Contain("`"));
        Assert.That(result, Does.Contain("Esempio:"));
    }

    [Test]
    public void SanitizeForSpeech_ReplacesMarkdownLinkWithLabel()
    {
        var result = TtsSpeechFormatter.SanitizeForSpeech("Vai su [Google](https://google.com) ora.");
        Assert.That(result, Is.EqualTo("Vai su Google ora."));
    }

    [Test]
    public void SanitizeForSpeech_RemovesListMarkersAndEmoji()
    {
        var result = TtsSpeechFormatter.SanitizeForSpeech("- primo\n- secondo 😀");
        Assert.That(result, Does.Not.Contain("-"));
        Assert.That(result, Does.Not.Contain("😀"));
    }
}
