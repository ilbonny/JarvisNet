using System.Runtime.InteropServices;
using JarvisNet.Audio.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.Alsa;

namespace JarvisNet.Audio.Internal;

internal static class AudioBackendFactory
{
    public static IWaveIn CreateWaveIn(AudioOptions options)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MMDevice? device = null;
            if (options.InputDeviceNumber >= 0)
            {
                var devices = new MMDeviceEnumerator()
                    .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .ToList();
                if (options.InputDeviceNumber < devices.Count)
                {
                    device = devices[options.InputDeviceNumber];
                }
            }

            var capture = device is null ? new WasapiCapture() : new WasapiCapture(device);
            capture.WaveFormat = new WaveFormat(options.SampleRate, 16, options.Channels);
            return capture;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var waveIn = new AlsaIn();
            waveIn.WaveFormat = new WaveFormat(options.SampleRate, 16, options.Channels);
            return waveIn;
        }

        throw new PlatformNotSupportedException("Audio input is not supported on this platform (macOS is not supported in v1).");
    }

    public static IWavePlayer CreateWavePlayer(AudioOptions options)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MMDevice? device = null;
            if (options.OutputDeviceNumber >= 0)
            {
                var devices = new MMDeviceEnumerator()
                    .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .ToList();
                if (options.OutputDeviceNumber < devices.Count)
                {
                    device = devices[options.OutputDeviceNumber];
                }
            }

            return device is null
                ? new WasapiOut(AudioClientShareMode.Shared, true, 100)
                : new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new AlsaOut();
        }

        throw new PlatformNotSupportedException("Audio output is not supported on this platform (macOS is not supported in v1).");
    }
}
