using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// How the Workspace Folder name suggested from a task is written (see <c>WorkspaceFolderNames.Suggest</c>). The default
/// is lower case, hyphenated, and starting with the task's reference, e.g. <c>jira-123-add-some-feature</c>.
/// </summary>
public sealed record WorkspaceFolderNameFormat
{
    /// <summary>
    /// What goes between words.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<WorkspaceFolderNameSeparator>))]
    public WorkspaceFolderNameSeparator Separator { get; init; } = WorkspaceFolderNameSeparator.Hyphens;

    /// <summary>
    /// The letter case.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<WorkspaceFolderNameCase>))]
    public WorkspaceFolderNameCase Case { get; init; } = WorkspaceFolderNameCase.Lowercase;

    /// <summary>
    /// Whether the name starts with the task's reference, e.g. <c>JIRA-123</c>.
    /// </summary>
    public bool IncludeTaskReference { get; init; } = true;

    /// <summary>
    /// Replaces values this version of Tammi doesn't know with the defaults.
    /// </summary>
    public WorkspaceFolderNameFormat Normalize() => this with
    {
        Separator = Enum.IsDefined(Separator) ? Separator : WorkspaceFolderNameSeparator.Hyphens,
        Case = Enum.IsDefined(Case) ? Case : WorkspaceFolderNameCase.Lowercase,
    };
}
