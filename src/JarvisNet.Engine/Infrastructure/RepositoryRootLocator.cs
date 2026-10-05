namespace JarvisNet.Engine.Infrastructure;

internal static class RepositoryRootLocator
{
    private const string SolutionMarker = "JarvisNet.slnx";

    public static string Find(string startDirectory)
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
}
