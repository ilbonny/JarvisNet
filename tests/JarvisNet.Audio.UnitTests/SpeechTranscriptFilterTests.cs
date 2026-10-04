using JarvisNet.Audio.Internal;

namespace JarvisNet.Audio.UnitTests;

[TestFixture]
public sealed class SpeechTranscriptFilterTests
{
    [TestCase(".", false)]
    [TestCase("  .  ", false)]
    [TestCase("a", false)]
    [TestCase("ok", true)]
    [TestCase("Ciao mondo", true)]
    public void IsMeaningfulFinalTranscript_FiltersNoise(string text, bool expected) =>
        Assert.That(SpeechTranscriptFilter.IsMeaningfulFinalTranscript(text), Is.EqualTo(expected));
}
