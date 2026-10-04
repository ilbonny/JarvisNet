namespace JarvisNet.Audio.Internal;

internal static class RepositoryPaths
{
    private const string SolutionMarker = "JarvisNet.slnx";

    public static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionMarker)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(startDirectory);
    }

    public static string GetModelsDirectory(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "models");

    public static string GetPiperDirectory(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "piper");
}
