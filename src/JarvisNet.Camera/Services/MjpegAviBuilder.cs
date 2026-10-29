using System.Buffers.Binary;
using System.Text;

namespace JarvisNet.Camera.Services;

public static class MjpegAviBuilder
{
    public static byte[] Build(IReadOnlyList<byte[]> jpegFrames, int framesPerSecond)
    {
        if (jpegFrames.Count == 0)
        {
            throw new ArgumentException("At least one JPEG frame is required.", nameof(jpegFrames));
        }

        framesPerSecond = Math.Clamp(framesPerSecond, 1, 30);
        var microSecPerFrame = 1_000_000 / framesPerSecond;

        using var moviPayload = new MemoryStream();
        var indexEntries = new List<(uint offset, uint size)>();
        foreach (var frame in jpegFrames)
        {
            var offset = (uint)moviPayload.Position + 8;
            WriteChunk(moviPayload, "00dc", frame);
            indexEntries.Add((offset, (uint)frame.Length));
        }

        var moviBytes = moviPayload.ToArray();
        var idx1Bytes = BuildIdx1(indexEntries);

        using var hdrlPayload = new MemoryStream();
        WriteAvih(hdrlPayload, microSecPerFrame, jpegFrames.Count);

        using var strlPayload = new MemoryStream();
        WriteStrh(strlPayload, microSecPerFrame, jpegFrames.Count);
        WriteStrf(strlPayload);
        WriteList(hdrlPayload, "strl", strlPayload.ToArray());

        using var body = new MemoryStream();
        WriteList(body, "hdrl", hdrlPayload.ToArray());
        WriteList(body, "movi", moviBytes);
        body.Write(idx1Bytes);

        using var riff = new MemoryStream();
        WriteFourCC(riff, "RIFF");
        var bodyBytes = body.ToArray();
        WriteInt32Le(riff, bodyBytes.Length + 4);
        WriteFourCC(riff, "AVI ");
        riff.Write(bodyBytes);
        return riff.ToArray();
    }

    private static byte[] BuildIdx1(List<(uint offset, uint size)> entries)
    {
        using var ms = new MemoryStream();
        WriteFourCC(ms, "idx1");
        WriteInt32Le(ms, entries.Count * 16);
        foreach (var (offset, size) in entries)
        {
            WriteFourCC(ms, "00dc");
            WriteInt32Le(ms, 0x10);
            WriteInt32Le(ms, (int)offset);
            WriteInt32Le(ms, (int)size);
        }

        return ms.ToArray();
    }

    private static void WriteAvih(Stream s, int microSecPerFrame, int totalFrames)
    {
        using var chunk = new MemoryStream();
        WriteInt32Le(chunk, microSecPerFrame);
        WriteInt32Le(chunk, 0);
        WriteInt32Le(chunk, 0);
        WriteInt32Le(chunk, 0);
        WriteInt32Le(chunk, 0x10);
        WriteInt32Le(chunk, totalFrames);
        WriteInt32Le(chunk, 0);
        WriteInt32Le(chunk, 1);
        WriteInt32Le(chunk, 640 * 480);
        WriteInt32Le(chunk, 640);
        WriteInt32Le(chunk, 480);
        chunk.Write(new byte[16]);
        WriteChunk(s, "avih", chunk.ToArray());
    }

    private static void WriteStrh(Stream s, int microSecPerFrame, int totalFrames)
    {
        using var chunk = new MemoryStream();
        WriteFourCC(chunk, "vids");
        WriteFourCC(chunk, "MJPG");
        WriteInt32Le(chunk, 0);
        chunk.Write(new byte[4]);
        WriteInt32Le(chunk, microSecPerFrame);
        WriteInt32Le(chunk, 1_000_000);
        WriteInt32Le(chunk, 0);
        WriteInt32Le(chunk, totalFrames);
        WriteInt32Le(chunk, 640 * 480);
        chunk.Write(new byte[32]);
        WriteChunk(s, "strh", chunk.ToArray());
    }

    private static void WriteStrf(Stream s)
    {
        using var chunk = new MemoryStream();
        WriteInt32Le(chunk, 40);
        WriteInt32Le(chunk, 640);
        WriteInt32Le(chunk, 480);
        chunk.Write(new byte[2]);
        WriteInt16Le(chunk, 24);
        WriteFourCC(chunk, "MJPG");
        WriteInt32Le(chunk, 640 * 480);
        chunk.Write(new byte[16]);
        WriteChunk(s, "strf", chunk.ToArray());
    }

    private static void WriteList(Stream s, string listType, byte[] content)
    {
        WriteFourCC(s, "LIST");
        WriteInt32Le(s, content.Length + 4);
        WriteFourCC(s, listType);
        s.Write(content);
    }

    private static void WriteChunk(Stream s, string fourCc, byte[] payload)
    {
        WriteFourCC(s, fourCc);
        WriteInt32Le(s, payload.Length);
        s.Write(payload);
        if (payload.Length % 2 == 1)
        {
            s.WriteByte(0);
        }
    }

    private static void WriteFourCC(Stream s, string fourCc)
    {
        var span = fourCc.AsSpan();
        var bytes = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            bytes[i] = i < span.Length ? (byte)span[i] : (byte)0;
        }

        s.Write(bytes);
    }

    private static void WriteInt32Le(Stream s, int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        s.Write(buf);
    }

    private static void WriteInt16Le(Stream s, short value)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(buf, value);
        s.Write(buf);
    }
}
