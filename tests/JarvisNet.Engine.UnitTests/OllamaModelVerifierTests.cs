using JarvisNet.Engine.Services;

namespace JarvisNet.Engine.UnitTests;

[TestFixture]
public sealed class OllamaModelVerifierTests
{
    [TestCase("jarvis:1b-new", "jarvis:1b-new", true)]
    [TestCase("jarvis:1b-new", "JARVIS:1b-new", true)]
    [TestCase("jarvis:1b-new", "jarvis:1b-new:latest", true)]
    [TestCase("JarvisNet:1b-new", "jarvis:1b-new", false)]
    public void ModelNamesMatch_ComparesOllamaTags(string configured, string installed, bool expected)
    {
        Assert.That(OllamaModelVerifier.ModelNamesMatch(configured, installed), Is.EqualTo(expected));
    }
}
