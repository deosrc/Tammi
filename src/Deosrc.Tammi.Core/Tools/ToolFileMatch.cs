namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// A file that a file tool's patterns found (see <see cref="ToolFileFinder"/>).
/// </summary>
/// <param name="FilePath">The file's full path.</param>
/// <param name="MatchFolder">
/// The full path of the folder the pattern matched from: the file's folder for a file name pattern (e.g.
/// <c>*.sln</c>), or the folder a path pattern starts in (e.g. the folder holding <c>.devcontainer/</c> for
/// <c>.devcontainer/devcontainer.json</c>).
/// </param>
public sealed record ToolFileMatch(string FilePath, string MatchFolder);
