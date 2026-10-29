namespace JarvisNet.Camera.Services;

/// <summary>Binary clip format from ESP32: JNETCLIP + frames (uint32 len + jpeg bytes).</summary>
public static class JpegClipCodec
{
    private static ReadOnlySpan<byte> Magic => "JNETCLIP"u8;

    public static bool TryDecode(ReadOnlySpan<byte> payload, out IReadOnlyList<byte[]> frames)
    {
        frames = [];
        if (payload.Length < Magic.Length + 3)
        {
            return false;
        }

        if (!payload[..Magic.Length].SequenceEqual(Magic))
        {
            return false;
        }

        var version = payload[Magic.Length];
        if (version != 1)
        {
            return false;
        }

        var frameCount = payload[Magic.Length + 1] | (payload[Magic.Length + 2] << 8);
        if (frameCount == 0)
        {
            return false;
        }

        var offset = Magic.Length + 3;
        var list = new List<byte[]>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            if (offset + 4 > payload.Length)
            {
                return false;
            }

            var len = (int)(uint)(
                payload[offset]
                | (payload[offset + 1] << 8)
                | (payload[offset + 2] << 16)
                | (payload[offset + 3] << 24));
            offset += 4;

            if (len <= 0 || offset + len > payload.Length)
            {
                return false;
            }

            var frame = payload.Slice(offset, len).ToArray();
            offset += len;
            list.Add(frame);
        }

        frames = list;
        return true;
    }

    public static byte[] BuildAviFromClip(ReadOnlySpan<byte> clipPayload, int framesPerSecond = 2)
    {
        if (!TryDecode(clipPayload, out var frames) || frames.Count == 0)
        {
            throw new InvalidDataException("Invalid JNETCLIP payload.");
        }

        return MjpegAviBuilder.Build(frames, Math.Max(1, framesPerSecond));
    }
}
