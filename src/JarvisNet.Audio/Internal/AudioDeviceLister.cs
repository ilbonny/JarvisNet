using System.Runtime.InteropServices;
using JarvisNet.Audio.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave.Alsa;

namespace JarvisNet.Audio.Internal;

internal static class AudioDeviceLister
{
    public static IReadOnlyList<AudioDeviceInfo> GetInputDevices()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return EnumerateWasapiInputs();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return EnumerateAlsaInputs();
        }

        return Array.Empty<AudioDeviceInfo>();
    }

    public static IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return EnumerateWasapiOutputs();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return EnumerateAlsaOutputs();
        }

        return Array.Empty<AudioDeviceInfo>();
    }

    private static IReadOnlyList<AudioDeviceInfo> EnumerateWasapiInputs()
    {
        var devices = new List<AudioDeviceInfo>();
        var index = 0;
        foreach (var device in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            devices.Add(new AudioDeviceInfo(index++, device.FriendlyName, IsInput: true));
        }

        return devices;
    }

    private static IReadOnlyList<AudioDeviceInfo> EnumerateWasapiOutputs()
    {
        var devices = new List<AudioDeviceInfo>();
        var index = 0;
        foreach (var device in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            devices.Add(new AudioDeviceInfo(index++, device.FriendlyName, IsInput: false));
        }

        return devices;
    }

    private static IReadOnlyList<AudioDeviceInfo> EnumerateAlsaInputs()
    {
        var devices = new List<AudioDeviceInfo>();
        var index = 0;
        foreach (var device in AlsaDeviceEnumerator.GetCaptureDevices())
        {
            devices.Add(new AudioDeviceInfo(index++, device.Name, IsInput: true));
        }

        return devices;
    }

    private static IReadOnlyList<AudioDeviceInfo> EnumerateAlsaOutputs()
    {
        var devices = new List<AudioDeviceInfo>();
        var index = 0;
        foreach (var device in AlsaDeviceEnumerator.GetPlaybackDevices())
        {
            devices.Add(new AudioDeviceInfo(index++, device.Name, IsInput: false));
        }

        return devices;
    }
}
