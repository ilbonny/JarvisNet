using JarvisNet.Audio;
using JarvisNet.Audio.Models;

namespace JarvisNet.Audio.UnitTests;

[TestFixture]
public sealed class PiperVoiceCatalogTests
{
    [Test]
    public void TryGetModelRelativePath_Leonardo_ReturnsMaleVoicePath()
    {
        Assert.That(
            PiperVoiceCatalog.TryGetModelRelativePath(PiperVoiceCatalog.LeonardoMedium, out var path),
            Is.True);
        Assert.That(path, Is.EqualTo("tts/it_IT-leonardo-medium.onnx"));
    }

    [Test]
    public void GetEffectivePiperModelRelativePath_UsesVoiceKeyOverModelPath()
    {
        var options = new AudioOptions
        {
            PiperVoiceKey = PiperVoiceCatalog.LeonardoMedium,
            PiperModelPath = "tts/it_IT-paola-medium.onnx",
        };

        Assert.That(
            options.GetEffectivePiperModelRelativePath(),
            Is.EqualTo("tts/it_IT-leonardo-medium.onnx"));
    }
}
