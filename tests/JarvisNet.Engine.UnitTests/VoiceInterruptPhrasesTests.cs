using JarvisNet.Engine.Services;

namespace JarvisNet.Engine.UnitTests;

[TestFixture]
public sealed class VoiceInterruptPhrasesTests
{
    [TestCase("stop", true)]
    [TestCase("Stop!", true)]
    [TestCase("basta", true)]
    [TestCase("fermati adesso", true)]
    [TestCase("ciao jarvis", false)]
    public void IsInterruptCommand_DetectsStopPhrases(string text, bool expected)
    {
        Assert.That(VoiceInterruptPhrases.IsInterruptCommand(text), Is.EqualTo(expected));
    }
}
