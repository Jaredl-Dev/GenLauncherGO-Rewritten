using System.IO;
using GenLauncherGO.Shared.IO;

namespace GenLauncherGO.Features.Integrity;

/// <summary>
///     Applies integrity-target containment to canonical lexical paths.
/// </summary>
internal static class ContentIntegrityPath
{
    /// <summary>
    ///     Gets a normalized relative path after proving that it does not leave the verified target.
    /// </summary>
    public static string GetRelativePath(string root, string path)
    {
        string relativePath = LexicalPath.GetRelativePath(root, path);
        if (LexicalPath.RelativePathLeavesRoot(relativePath))
        {
            throw new InvalidDataException("A scanned entry resolved outside the verified target.");
        }

        return relativePath;
    }

    /// <summary>
    ///     Resolves an integrity issue path after proving that it remains in the verified target.
    /// </summary>
    public static string ResolveRelativePath(string root, string relativePath)
    {
        return LexicalPath.ResolveContainedPath(
            root,
            relativePath,
            "An integrity issue path resolved outside its target root.");
    }
}
