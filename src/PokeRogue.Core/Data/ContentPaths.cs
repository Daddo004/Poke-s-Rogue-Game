namespace PokeRogue.Core.Data;

/// <summary>Locates the <c>content</c> folder: next to the executable when published, or at the repository root during development.</summary>
public static class ContentPaths
{
    public const string ContentFolderName = "content";

    public static string FindContentRoot(string? startDirectory = null)
    {
        var dir = new DirectoryInfo(startDirectory ?? AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ContentFolderName);
            if (Directory.Exists(candidate) && (Directory.Exists(Path.Combine(candidate, "data")) || Directory.Exists(Path.Combine(candidate, "generated"))))
                return candidate;

            if (File.Exists(Path.Combine(dir.FullName, "PokeRogue.sln")))
                return candidate;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find a '{ContentFolderName}' folder above {startDirectory ?? AppContext.BaseDirectory}.");
    }

    public static string Generated(string contentRoot, string fileName) => Path.Combine(contentRoot, "generated", fileName);

    public static string SpriteFolder(string contentRoot, int speciesId) => Path.Combine(contentRoot, "sprites", speciesId.ToString("D4"));

    public static string Portrait(string contentRoot, int speciesId) => Path.Combine(contentRoot, "portraits", $"{speciesId:D4}.png");
}
