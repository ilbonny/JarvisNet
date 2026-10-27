using System.Runtime.InteropServices;
using Concentus.Oggfile;
using Concentus.Structs;

namespace JarvisNet.Audio.Internal;

internal static class OggOpusAudioDecoder
{
    public static (float[] Samples, int SampleRate) DecodeToMonoFloat(Stream oggOpusStream)
    {
        const int opusSampleRate = 48_000;
        var decoder = new OpusDecoder(opusSampleRate, 1);
        var reader = new OpusOggReadStream(decoder, oggOpusStream);
        var pcm = new List<short>();

        while (reader.HasNextPacket)
        {
            var packet = reader.DecodeNextPacket();
            if (packet is { Length: > 0 })
            {
                pcm.AddRange(packet);
            }
        }

        if (pcm.Count == 0)
        {
            return ([], opusSampleRate);
        }

        return (AudioPcmConverter.ShortsToFloatSamples(CollectionsMarshal.AsSpan(pcm)), opusSampleRate);
    }
}
