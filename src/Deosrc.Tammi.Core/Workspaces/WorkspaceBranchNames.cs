using TAMMi.Core.Integrations;
using TAMMi.Core.Settings;

namespace TAMMi.Core.Workspaces;

/// <summary>
/// Suggests the name of the branch a workspace creates in each of its repositories.
/// </summary>
public static class WorkspaceBranchNames
{
    private static readonly WorkspaceNameFormat Format = new()
    {
        Separator = WorkspaceNameSeparator.Hyphens,
        Case = WorkspaceNameCase.AsWritten,
        IncludeTaskReference = true,
    };

    /// <summary>
    /// Suggests a branch name: <c>&lt;task-key&gt;-&lt;slug&gt;</c> from the task when there's exactly one, and
    /// otherwise a slug of the workspace's name.
    /// </summary>
    /// <remarks>
    /// The task key is its short reference (see <see cref="WorkspaceNames.ShortReference"/>) as written, so a JIRA
    /// issue's key stays uppercase and links the branch to it. The slug is lowercase words joined by hyphens, leaving
    /// out punctuation. So <c>ABC-123</c> "Add some feature" suggests <c>ABC-123-add-some-feature</c>, and
    /// <c>acme/api#42</c> "Fix login" suggests <c>api-42-fix-login</c>. Like a suggested workspace name, it's at most
    /// <see cref="WorkspaceNames.MaxSuggestedLength"/> characters.
    /// </remarks>
    /// <returns>The suggested name, or <see langword="null"/> if there's none (e.g. the workspace has no name yet).</returns>
    public static string? Suggest(IReadOnlyList<TaskInfo> tasks, string? workspaceName)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        return tasks is [var task]
            ? WorkspaceNames.FromTitle(task.Title.ToLowerInvariant(), Format, WorkspaceNames.ShortReference(task.Key))
            : WorkspaceNames.FromTitle(workspaceName?.ToLowerInvariant(), Format);
    }
}
