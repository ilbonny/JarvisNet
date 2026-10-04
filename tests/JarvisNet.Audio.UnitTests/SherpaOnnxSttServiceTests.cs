using JarvisNet.Audio.Models;
using JarvisNet.Audio.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JarvisNet.Audio.UnitTests;

[TestFixture]
public sealed class SherpaOnnxSttServiceTests
{
    [Test]
    public void ProcessAudio_IgnoresSamples_WhenSessionNotStarted()
    {
        var options = Options.Create(new AudioOptions
        {
            SherpaEncoderPath = TestFileHelper.CreateTempFile("enc"),
            SherpaDecoderPath = TestFileHelper.CreateTempFile("dec"),
            SherpaJoinerPath = TestFileHelper.CreateTempFile("join"),
            SherpaTokensPath = TestFileHelper.CreateTempFile("tok", ".txt"),
        });

        var service = new SherpaOnnxSttService(options, NullLogger<SherpaOnnxSttService>.Instance);
        var eventRaised = false;
        service.SpeechRecognized += (_, _) => eventRaised = true;

        service.ProcessAudio(new float[] { 0.1f, 0.2f, 0.3f }, 16000);

        Assert.That(eventRaised, Is.False);
    }
}
