using System.Text.RegularExpressions;
using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Suggests and checks workspace names. A workspace's name is free text, shown on Home and its tab. It's separate from
/// its Workspace Folder's name (see <see cref="WorkspaceFolderNames"/>).
/// </summary>
/// <remarks>
/// A name is suggested from a template, e.g. <c>{TaskReference} {TaskTitle}</c>, whose placeholders are filled in from
/// the workspace's task. Placeholders are written in braces and matched ignoring case. Text in braces that isn't a
/// placeholder is left as it is.
/// </remarks>
public static partial class WorkspaceNames
{
    /// <summary>
    /// The task's full key, e.g. <c>acme/api#42</c> or <c>ABC-123</c>.
    /// </summary>
    public const string TaskKey = "TaskKey";

    /// <summary>
    /// The task's short reference (see <see cref="TaskReference.ShortKey"/>), e.g. <c>api#42</c> or <c>ABC-123</c>.
    /// </summary>
    public const string TaskReference = "TaskReference";

    /// <summary>
    /// The task's title, e.g. <c>Fix login</c>.
    /// </summary>
    public const string TaskTitle = "TaskTitle";

    /// <summary>
    /// The owner of the repository the task belongs to, e.g. <c>acme</c> for <c>acme/api#42</c>. Only for tasks that
    /// belong to a repository, such as GitHub issues.
    /// </summary>
    public const string RepositoryOwner = "RepositoryOwner";

    /// <summary>
    /// The name of the repository the task belongs to, e.g. <c>api</c> for <c>acme/api#42</c>. Only for tasks that
    /// belong to a repository, such as GitHub issues.
    /// </summary>
    public const string RepositoryName = "RepositoryName";

    /// <summary>
    /// Every placeholder, in the order they're listed on the Settings screen.
    /// </summary>
    public static IReadOnlyList<string> Placeholders { get; } =
        [TaskReference, TaskKey, TaskTitle, RepositoryOwner, RepositoryName];

    private static readonly HashSet<string> Known = new(Placeholders, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Writes a placeholder as it's used in the template, e.g. <c>{TaskTitle}</c>.
    /// </summary>
    public static string Format(string name) => "{" + name + "}";

    /// <summary>
    /// Suggests a name for a workspace with these tasks: <paramref name="template"/> filled in from the task, when
    /// there's exactly one.
    /// </summary>
    /// <returns>
    /// The suggested name, or <see langword="null"/> if there's none: there isn't exactly one task, the template is
    /// blank, it uses a placeholder the task has no value for (e.g. <see cref="RepositoryName"/> for a JIRA issue), or
    /// it leaves nothing but spaces. The Create Workspace flow then uses the Workspace Folder's name.
    /// </returns>
    public static string? Suggest(IReadOnlyList<TaskInfo> tasks, string? template)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks is not [var task] || string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [TaskKey] = task.Key,
            [TaskReference] = Integrations.TaskReference.ShortKey(task.Key),
            [TaskTitle] = task.Title,
            [RepositoryOwner] = RepositoryPart(task.RepositoryUrl, fromEnd: 2),
            [RepositoryName] = RepositoryPart(task.RepositoryUrl, fromEnd: 1),
        };

        var missing = false;
        string expanded = Placeholder().Replace(template, match =>
        {
            if (!values.TryGetValue(match.Groups[1].Value, out string? value))
            {
                return match.Value;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                missing = true;
                return match.Value;
            }

            return value;
        });

        // Values can hold line breaks and runs of spaces (e.g. a title pasted from somewhere), which a name shouldn't.
        string name = Whitespace().Replace(expanded, " ").Trim();
        return missing || name.Length == 0 ? null : name;
    }

    /// <summary>
    /// The text in braces in <paramref name="template"/> that isn't a placeholder, e.g. a mistyped one.
    /// </summary>
    public static IEnumerable<string> FindUnknown(string? template) =>
        Placeholder().Matches(template ?? string.Empty)
            .Select(match => match.Groups[1].Value)
            .Where(name => !Known.Contains(name))
            .Distinct();

    /// <summary>
    /// Returns why <paramref name="name"/> can't be a workspace's name, or <see langword="null"/> if it can.
    /// Surrounding spaces are ignored, because they're removed when the name is saved.
    /// </summary>
    public static string? Validate(string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return "Enter a name for the workspace.";
        }

        if (name.Any(char.IsControl))
        {
            return "The name can't contain line breaks, tabs or other control characters.";
        }

        return null;
    }

    /// <summary>
    /// A segment of a repository's web page path, counted from the end: 1 for its name, 2 for its owner.
    /// </summary>
    private static string? RepositoryPart(Uri? repositoryUrl, int fromEnd)
    {
        if (repositoryUrl is null)
        {
            return null;
        }

        string[] segments = repositoryUrl.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? Uri.UnescapeDataString(segments[^fromEnd]) : null;
    }

    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
