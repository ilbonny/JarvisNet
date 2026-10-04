using System.Runtime.InteropServices;
using JarvisNet.Audio.Models;

namespace JarvisNet.Audio.Internal;

internal static class AudioPathHelper
{
    public static string ResolvePiperExecutablePath(AudioOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PiperExecutablePath))
        {
            return options.ResolvePath(options.PiperExecutablePath);
        }

        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "piper.exe" : "piper";
        var workingDirectory = string.IsNullOrWhiteSpace(options.PiperWorkingDirectory)
            ? RepositoryPaths.GetPiperDirectory(RepositoryPaths.FindRepositoryRoot(AppContext.BaseDirectory))
            : options.PiperWorkingDirectory;
        return Path.Combine(workingDirectory, fileName);
    }
}
