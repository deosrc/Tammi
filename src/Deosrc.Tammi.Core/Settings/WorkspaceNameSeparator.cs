namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// What goes between the words of a suggested workspace name.
/// </summary>
public enum WorkspaceNameSeparator
{
    /// <summary>
    /// <c>jira-123-add-some-feature</c>. Punctuation is left out.
    /// </summary>
    Hyphens,

    /// <summary>
    /// <c>jira_123_add_some_feature</c>. Punctuation is left out.
    /// </summary>
    Underscores,

    /// <summary>
    /// <c>JIRA-123 Add some feature</c>. Punctuation is kept, except characters a folder name can't have.
    /// </summary>
    Spaces,
}
