using JarvisNet.Audio.Internal;
using JarvisNet.Audio.Models;

namespace JarvisNet.Audio;

public static class AudioDevices
{
    public static IReadOnlyList<AudioDeviceInfo> GetInputDevices() => AudioDeviceLister.GetInputDevices();

    public static IReadOnlyList<AudioDeviceInfo> GetOutputDevices() => AudioDeviceLister.GetOutputDevices();
}
