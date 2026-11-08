using System.Text;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Suggests and checks the names of Workspace Folders, which must be valid folder names on every platform. The
/// workspace's own name is separate (see <see cref="WorkspaceNames"/>).
/// </summary>
public static class WorkspaceFolderNames
{
    /// <summary>
    /// The longest name <see cref="Suggest"/> makes, in characters. Longer titles are cut at a word, so the Workspace
    /// Folder's path stays a manageable length. The task's reference is always kept.
    /// </summary>
    public const int MaxSuggestedLength = 80;

    /// <summary>
    /// Suggests a Workspace Folder name for a workspace with these tasks: the task's title (and reference) written in
    /// <paramref name="format"/>, when there's exactly one task. With no tasks or several, there's no suggestion.
    /// </summary>
    /// <returns>The suggested name, or <see langword="null"/> if there's none.</returns>
    public static string? Suggest(IReadOnlyList<TaskInfo> tasks, WorkspaceFolderNameFormat format)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(format);
        return tasks is [var task]
            ? FromTitle(task.Title, format, format.IncludeTaskReference ? TaskReference.ShortKey(task.Key) : null)
            : null;
    }

    /// <summary>
    /// Writes a task's title (or any other text), after its reference if there's one, as a folder name in
    /// <paramref name="format"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With hyphens or underscores, the words are the runs of letters and digits, so punctuation is left out
    /// (apostrophes are dropped, so "don't" stays one word). With spaces, punctuation is kept, except characters a
    /// folder name can't have, which separate words.
    /// </para>
    /// <para>
    /// A name longer than <see cref="MaxSuggestedLength"/> is cut at the last word that fits, or within a word if that
    /// would leave too little of the title.
    /// </para>
    /// </remarks>
    /// <param name="title">The task's title.</param>
    /// <param name="format">How to write the name.</param>
    /// <param name="reference">The task's reference to start with, e.g. <c>ABC-123</c>, or <see langword="null"/>.</param>
    /// <returns>The name, or <see langword="null"/> if nothing of the title and reference is left.</returns>
    public static string? FromTitle(string? title, WorkspaceFolderNameFormat format, string? reference = null)
    {
        ArgumentNullException.ThrowIfNull(format);
        string separator = format.Separator switch
        {
            WorkspaceFolderNameSeparator.Underscores => "_",
            WorkspaceFolderNameSeparator.Spaces => " ",
            _ => "-",
        };

        List<string> referenceWords = Words(reference, format);
        List<string> titleWords = Words(title, format);
        var name = new StringBuilder(string.Join(separator, referenceWords));
        foreach (string word in titleWords)
        {
            int length = name.Length + (name.Length > 0 ? separator.Length : 0) + word.Length;
            if (length > MaxSuggestedLength)
            {
                // Cut within the word unless enough of the name is already there.
                if (name.Length < MaxSuggestedLength / 2)
                {
                    if (name.Length > 0)
                    {
                        name.Append(separator);
                    }

                    name.Append(word.AsSpan(0, MaxSuggestedLength - name.Length));
                }

                break;
            }

            if (name.Length > 0)
            {
                name.Append(separator);
            }

            name.Append(word);
        }

        string result = name.ToString().Trim().TrimEnd('.', ' ');
        result = format.Case switch
        {
            WorkspaceFolderNameCase.Lowercase => result.ToLowerInvariant(),
            WorkspaceFolderNameCase.Uppercase => result.ToUpperInvariant(),
            _ => result,
        };
        return FolderNames.IsValid(result) ? result : null;
    }

    private static List<string> Words(string? text, WorkspaceFolderNameFormat format)
    {
        var words = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return words;
        }

        bool spaces = format.Separator == WorkspaceFolderNameSeparator.Spaces;
        var word = new StringBuilder();
        foreach (char c in text)
        {
            bool separates = spaces
                ? char.IsWhiteSpace(c) || FolderNames.ContainsInvalidCharacter(c.ToString())
                : !char.IsLetterOrDigit(c) && c is not ('\'' or '\u2019');
            if (!separates)
            {
                // Apostrophes join words in hyphenated and underscored names, but don't appear in them.
                if (spaces || c is not ('\'' or '\u2019'))
                {
                    word.Append(c);
                }
            }
            else if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }

        if (word.Length > 0)
        {
            words.Add(word.ToString());
        }

        return words;
    }

    /// <summary>
    /// Returns why <paramref name="name"/> can't be a Workspace Folder's name, or <see langword="null"/> if it can.
    /// Surrounding spaces are ignored, because they're removed when the workspace is created.
    /// </summary>
    public static string? Validate(string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return "Enter a name for the Workspace Folder.";
        }

        if (name is "." or "..")
        {
            return "The folder name can't be '.' or '..'.";
        }

        if (FolderNames.ContainsInvalidCharacter(name))
        {
            return $"The folder name can't contain {string.Join(' ', FolderNames.InvalidCharacters)} or control "
                + "characters.";
        }

        if (name.EndsWith('.'))
        {
            return "The folder name can't end with a full stop, because Windows doesn't allow it.";
        }

        if (FolderNames.IsReservedOnWindows(name))
        {
            return $"'{name}' can't be a folder name on Windows, which reserves it for a device.";
        }

        if (FolderNames.IsTooLong(name))
        {
            return "The folder name is too long.";
        }

        return null;
    }
}
