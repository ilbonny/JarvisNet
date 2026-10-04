using JarvisNet.Audio.Internal;
using JarvisNet.Audio.Models;

namespace JarvisNet.Audio.UnitTests;

[TestFixture]
public sealed class AudioPcmConverterTests
{
    [Test]
    public void ShortsToFloatSamples_NormalizesToUnitRange()
    {
        var samples = new short[] { 0, short.MaxValue, short.MinValue };

        var floats = AudioPcmConverter.ShortsToFloatSamples(samples);

        Assert.That(floats[0], Is.EqualTo(0f).Within(0.0001));
        Assert.That(floats[1], Is.EqualTo(1f).Within(0.01));
        Assert.That(floats[2], Is.EqualTo(-1f).Within(0.01));
    }

    [Test]
    public void ComputeRms_ReturnsZero_ForSilentBuffer()
    {
        var rms = AudioPcmConverter.ComputeRms(new float[] { 0, 0, 0, 0 });
        Assert.That(rms, Is.EqualTo(0f));
    }

    [Test]
    public void ShouldForwardFrame_PushToTalk_RequiresActiveFlag()
    {
        var gate = false;

        Assert.That(
            AudioPcmConverter.ShouldForwardFrame(AudioInputMode.PushToTalk, false, 0.5f, 0.01f, ref gate),
            Is.False);
        Assert.That(
            AudioPcmConverter.ShouldForwardFrame(AudioInputMode.PushToTalk, true, 0.001f, 0.01f, ref gate),
            Is.True);
    }

    [Test]
    public void ShouldForwardFrame_Vad_OpensOnEnergyThreshold()
    {
        var gate = false;

        Assert.That(
            AudioPcmConverter.ShouldForwardFrame(AudioInputMode.Vad, false, 0.02f, 0.01f, ref gate),
            Is.True);
        Assert.That(gate, Is.True);
    }
}
