using JarvisNet.Audio.Models;

namespace JarvisNet.Audio.UnitTests;

[TestFixture]
public sealed class AudioOptionsValidatorTests
{
    private readonly AudioOptionsValidator _validator = new();

    [Test]
    public void Validate_ReturnsFailure_WhenSampleRateIsInvalid()
    {
        var options = CreateValidOptions();
        options.SampleRate = 0;

        var result = _validator.Validate(null, options);

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures, Has.Some.Contains("SampleRate"));
    }

    [Test]
    public void Validate_ReturnsFailure_WhenSherpaPathsMissing()
    {
        var options = CreateValidOptions();
        options.SherpaEncoderPath = string.Empty;

        var result = _validator.Validate(null, options);

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures, Has.Some.Contains("SherpaEncoderPath"));
    }

    [Test]
    public void Validate_Succeeds_WhenAllPathsExist()
    {
        var options = CreateValidOptions();

        var result = _validator.Validate(null, options);

        Assert.That(result.Succeeded, Is.True);
    }

    private static AudioOptions CreateValidOptions()
    {
        var encoder = TestFileHelper.CreateTempFile("encoder");
        var decoder = TestFileHelper.CreateTempFile("decoder");
        var joiner = TestFileHelper.CreateTempFile("joiner");
        var tokens = TestFileHelper.CreateTempFile("tokens", ".txt");
        var piperModel = TestFileHelper.CreateTempFile("piper-voice");

        return new AudioOptions
        {
            SampleRate = 16000,
            Channels = 1,
            PiperModelPath = piperModel,
            SherpaEncoderPath = encoder,
            SherpaDecoderPath = decoder,
            SherpaJoinerPath = joiner,
            SherpaTokensPath = tokens,
        };
    }
}
