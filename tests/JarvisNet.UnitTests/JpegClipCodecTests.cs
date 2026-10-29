using JarvisNet.Camera.Services;

namespace JarvisNet.UnitTests;

[TestFixture]
public sealed class JpegClipCodecTests
{
    [Test]
    public void BuildAviFromClip_ProducesNonEmptyAvi()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        using var ms = new MemoryStream();
        ms.Write("JNETCLIP"u8);
        ms.WriteByte(1);
        ms.WriteByte(1);
        ms.WriteByte(0);
        ms.Write(new byte[] { 4, 0, 0, 0 });
        ms.Write(jpeg);

        var avi = JpegClipCodec.BuildAviFromClip(ms.ToArray(), framesPerSecond: 2);
        Assert.That(avi.Length, Is.GreaterThan(64));
        Assert.That(avi.AsSpan(0, 4).SequenceEqual("RIFF"u8), Is.True);
    }
}
