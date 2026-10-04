namespace JarvisNet.Audio.UnitTests;

internal static class TestFileHelper
{
    public static string CreateTempFile(string prefix, string extension = ".onnx")
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, "test");
        return path;
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }
}
