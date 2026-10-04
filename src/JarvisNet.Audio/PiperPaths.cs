using JarvisNet.Audio.Internal;
using JarvisNet.Audio.Models;

namespace JarvisNet.Audio;

public static class PiperPaths
{
    public static string ResolveExecutablePath(AudioOptions options) =>
        AudioPathHelper.ResolvePiperExecutablePath(options);
}
