using System.Text.RegularExpressions;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// The placeholders in a tool's name and arguments, e.g. <c>{PreferredFolder}</c>, which are filled in for each folder or
/// file the tool opens.
/// </summary>
/// <remarks>
/// Placeholders are written in braces and matched ignoring case. Text in braces that isn't a placeholder is left as it
/// is.
/// </remarks>
public static partial class ToolPlaceholders
{
    /// <summary>
    /// The folder the tool should open: <see cref="LaunchFolder"/>, except for the Workspace Folder of a workspace with
    /// only one repository, where it's that repository's Workspace Worktree unless the settings say otherwise (see
    /// <see cref="SingleRepositoryTarget"/>). The predefined folder tools open it.
    /// </summary>
    public const string PreferredFolder = "PreferredFolder";

    /// <summary>
    /// The folder the entry is offered for: the Workspace Folder, or the repository's Workspace Worktree.
    /// </summary>
    public const string LaunchFolder = "LaunchFolder";

    /// <summary>
    /// The Workspace Folder's full path.
    /// </summary>
    public const string WorkspaceFolder = "WorkspaceFolder";

    /// <summary>
    /// The full path of the repository's Workspace Worktree: the one the entry is offered for, or the one the file is
    /// in. Only for entries of a repository.
    /// </summary>
    public const string RepositoryFolder = "RepositoryFolder";

    /// <summary>
    /// The full path of the folder the file is in. Only for file tools.
    /// </summary>
    public const string FileDirectory = "FileDirectory";

    /// <summary>
    /// The file's name, e.g. <c>Api.sln</c>. Only for file tools.
    /// </summary>
    public const string FileName = "FileName";

    /// <summary>
    /// The file's name without its extension, e.g. <c>Api</c>. Only for file tools.
    /// </summary>
    public const string FileNameWithoutExtension = "FileNameWithoutExtension";

    /// <summary>
    /// The file's full path: <see cref="FileDirectory"/> and <see cref="FileName"/> combined. Only for file tools.
    /// </summary>
    public const string FilePath = "FilePath";

    /// <summary>
    /// The full path of the folder the file pattern matched from: the file's folder for a file name pattern (e.g.
    /// <c>*.sln</c>), or the folder a path pattern starts in (e.g. the folder holding <c>.devcontainer/</c> for
    /// <c>.devcontainer/devcontainer.json</c>). Only for file tools.
    /// </summary>
    public const string MatchFolder = "MatchFolder";

    /// <summary>
    /// Every placeholder, in the order they're listed on the Settings screen.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        PreferredFolder, LaunchFolder, WorkspaceFolder, RepositoryFolder, FileDirectory, FileName,
        FileNameWithoutExtension, FilePath, MatchFolder,
    ];

    private static readonly HashSet<string> Known = new(All, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The placeholders a tool can have. A placeholder that's only filled in for some entries (e.g.
    /// <see cref="RepositoryFolder"/> in a folder tool) is included: entries without it aren't offered.
    /// </summary>
    public static IReadOnlyList<string> For(ToolKind kind) => kind == ToolKind.File
        ? All
        : [PreferredFolder, LaunchFolder, WorkspaceFolder, RepositoryFolder];

    /// <summary>
    /// Writes a placeholder as it's used in a tool's settings, e.g. <c>{PreferredFolder}</c>.
    /// </summary>
    public static string Format(string name) => "{" + name + "}";

    /// <summary>
    /// The names of the placeholders used in <paramref name="text"/>, as they're written in <see cref="All"/>.
    /// </summary>
    public static IEnumerable<string> FindUsed(string text) =>
        Placeholder().Matches(text ?? string.Empty)
            .Select(match => match.Groups[1].Value)
            .Where(Known.Contains)
            .Select(name => All.First(known => known.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .Distinct();

    /// <summary>
    /// The text in braces in <paramref name="text"/> that isn't a placeholder, e.g. a mistyped one.
    /// </summary>
    public static IEnumerable<string> FindUnknown(string text) =>
        Placeholder().Matches(text ?? string.Empty)
            .Select(match => match.Groups[1].Value)
            .Where(name => !Known.Contains(name))
            .Distinct();

    /// <summary>
    /// Fills in the placeholders in <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text, e.g. an argument.</param>
    /// <param name="values">The placeholders' values, by name, ignoring case.</param>
    /// <returns>
    /// The text with each placeholder replaced, or <c>null</c> if it uses a placeholder that has no value here (e.g.
    /// <see cref="RepositoryFolder"/> for the Workspace Folder). Values are put in as they are: placeholders in them
    /// aren't filled in.
    /// </returns>
    public static string? Expand(string text, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(values);

        var missing = false;
        var expanded = Placeholder().Replace(text, match =>
        {
            var name = match.Groups[1].Value;
            if (!Known.Contains(name))
            {
                return match.Value;
            }

            var value = values.FirstOrDefault(entry => entry.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (value.Value is null)
            {
                missing = true;
                return match.Value;
            }

            return value.Value;
        });
        return missing ? null : expanded;
    }

    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex Placeholder();
}
