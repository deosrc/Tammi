using System.Text;
using TAMMi.Core.Integrations;
using TAMMi.Core.Platform;

namespace TAMMi.Core.Workspaces;

/// <summary>
/// Suggests and checks workspace names. A workspace's name is also its Workspace Folder's name, so it must be a valid
/// folder name on every platform.
/// </summary>
public static class WorkspaceNames
{
    /// <summary>
    /// The longest name <see cref="Suggest"/> makes from a task's title, in characters. Longer titles are cut at a
    /// word, so the Workspace Folder's path stays a manageable length.
    /// </summary>
    public const int MaxSuggestedLength = 80;

    /// <summary>
    /// Suggests a name for a workspace with these tasks: the task's title when there's exactly one, made into a
    /// valid folder name (see <see cref="FromTitle"/>). With no tasks or several, the user enters the name.
    /// </summary>
    /// <returns>The suggested name, or <see langword="null"/> if there's none.</returns>
    public static string? Suggest(IReadOnlyList<TaskInfo> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        return tasks is [var task] ? FromTitle(task.Title) : null;
    }

    /// <summary>
    /// Makes a task's title into a workspace name that's a valid folder name: characters that aren't allowed in a
    /// folder name become spaces, runs of spaces become one, and surrounding spaces and trailing full stops are
    /// removed. A name longer than <see cref="MaxSuggestedLength"/> is cut at the last word that fits.
    /// </summary>
    /// <returns>The name, or <see langword="null"/> if nothing of the title is left.</returns>
    public static string? FromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var name = new StringBuilder(title.Length);
        foreach (char c in title)
        {
            bool space = char.IsWhiteSpace(c) || FolderNames.ContainsInvalidCharacter(c.ToString());
            if (!space)
            {
                name.Append(c);
            }
            else if (name.Length > 0 && name[^1] != ' ')
            {
                name.Append(' ');
            }
        }

        string result = Tidy(name.ToString());
        if (result.Length > MaxSuggestedLength)
        {
            string cut = result[..MaxSuggestedLength];
            int lastSpace = cut.LastIndexOf(' ');

            // Cut at a word unless that leaves too little of the title.
            result = Tidy(lastSpace >= MaxSuggestedLength / 2 ? cut[..lastSpace] : cut);
        }

        return FolderNames.IsValid(result) ? result : null;
    }

    /// <summary>
    /// Returns why <paramref name="name"/> can't be a workspace's name, or <see langword="null"/> if it can.
    /// Surrounding spaces are ignored, because they're removed when the workspace is created.
    /// </summary>
    public static string? Validate(string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return "Enter a name for the workspace.";
        }

        if (name is "." or "..")
        {
            return "The name can't be '.' or '..'.";
        }

        if (FolderNames.ContainsInvalidCharacter(name))
        {
            return $"The name can't contain {string.Join(' ', FolderNames.InvalidCharacters)} or control characters, "
                + "because it's also the Workspace Folder's name.";
        }

        if (name.EndsWith('.'))
        {
            return "The name can't end with a full stop, because Windows doesn't allow it in a folder name.";
        }

        if (FolderNames.IsReservedOnWindows(name))
        {
            return $"'{name}' can't be a folder name on Windows, which reserves it for a device.";
        }

        if (FolderNames.IsTooLong(name))
        {
            return "The name is too long for a folder name.";
        }

        return null;
    }

    private static string Tidy(string name) => name.Trim().TrimEnd('.', ' ');
}
