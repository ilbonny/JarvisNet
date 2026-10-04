using JarvisNet.Engine.Services;

namespace JarvisNet.Engine.UnitTests;

[TestFixture]
public sealed class JarvisNetOrchestratorTests
{
    [TestCase("esci", true)]
    [TestCase("Esci.", true)]
    [TestCase("EXIT", true)]
    [TestCase("ciao mondo", false)]
    public void IsExitCommand_DetectsExitPhrases(string text, bool expected)
    {
        Assert.That(JarvisNetOrchestrator.IsExitCommand(text), Is.EqualTo(expected));
    }

    [TestCase("a", false)]
    [TestCase("ok", true)]
    [TestCase("  ciao  ", true)]
    public void IsMeaningfulFinalTranscript_FiltersNoise(string text, bool expected)
    {
        Assert.That(JarvisNetOrchestrator.IsMeaningfulFinalTranscript(text), Is.EqualTo(expected));
    }
}
