using System.Text;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Suggests and checks workspace names. A workspace's name is also its Workspace Folder's name, so it must be a valid
/// folder name on every platform.
/// </summary>
public static class WorkspaceNames
{
    /// <summary>
    /// The longest name <see cref="Suggest"/> makes, in characters. Longer titles are cut at a word, so the Workspace
    /// Folder's path stays a manageable length. The task's reference is always kept.
    /// </summary>
    public const int MaxSuggestedLength = 80;

    /// <summary>
    /// Suggests a name for a workspace with these tasks: the task's title (and reference) written in
    /// <paramref name="format"/>, when there's exactly one task. With no tasks or several, the user enters the name.
    /// </summary>
    /// <returns>The suggested name, or <see langword="null"/> if there's none.</returns>
    public static string? Suggest(IReadOnlyList<TaskInfo> tasks, WorkspaceNameFormat format)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(format);
        return tasks is [var task]
            ? FromTitle(task.Title, format, format.IncludeTaskReference ? ShortReference(task.Key) : null)
            : null;
    }

    /// <summary>
    /// The part of a task's key used in its suggested name: the key after its last <c>/</c>, so a GitHub issue
    /// <c>acme/api#42</c> gives <c>api#42</c>, and a JIRA issue <c>ABC-123</c> stays <c>ABC-123</c>.
    /// </summary>
    public static string ShortReference(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key[(key.LastIndexOf('/') + 1)..];
    }

    /// <summary>
    /// Writes a task's title, after its reference if there's one, as a workspace name in <paramref name="format"/>
    /// that's a valid folder name.
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
    public static string? FromTitle(string? title, WorkspaceNameFormat format, string? reference = null)
    {
        ArgumentNullException.ThrowIfNull(format);
        string separator = format.Separator switch
        {
            WorkspaceNameSeparator.Underscores => "_",
            WorkspaceNameSeparator.Spaces => " ",
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
            WorkspaceNameCase.Lowercase => result.ToLowerInvariant(),
            WorkspaceNameCase.Uppercase => result.ToUpperInvariant(),
            _ => result,
        };
        return FolderNames.IsValid(result) ? result : null;
    }

    private static List<string> Words(string? text, WorkspaceNameFormat format)
    {
        var words = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return words;
        }

        bool spaces = format.Separator == WorkspaceNameSeparator.Spaces;
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
}
