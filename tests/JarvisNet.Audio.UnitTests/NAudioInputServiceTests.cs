using JarvisNet.Audio.Models;
using JarvisNet.Audio.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
namespace JarvisNet.Audio.UnitTests;

[TestFixture]
public sealed class NAudioInputServiceTests
{
    [Test]
    public void SetPushToTalkActive_UpdatesModeFlag()
    {
        var options = Options.Create(new AudioOptions { InputMode = AudioInputMode.PushToTalk });
        var service = new NAudioInputService(options, NullLogger<NAudioInputService>.Instance);

        service.SetPushToTalkActive(true);

        Assert.That(service.InputMode, Is.EqualTo(AudioInputMode.PushToTalk));
    }

    [Test]
    public async Task GetInputDevicesAsync_DoesNotThrow_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("Windows-specific enumeration test.");
        }

        var options = Options.Create(new AudioOptions());
        var service = new NAudioInputService(options, NullLogger<NAudioInputService>.Instance);

        var devices = await service.GetInputDevicesAsync();

        Assert.That(devices, Is.Not.Null);
    }
}
