using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// How the workspace name suggested from a task is written (see <c>WorkspaceNames.Suggest</c>). The default is lower
/// case, hyphenated, and starting with the task's reference, e.g. <c>jira-123-add-some-feature</c>.
/// </summary>
public sealed record WorkspaceNameFormat
{
    /// <summary>
    /// What goes between words.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<WorkspaceNameSeparator>))]
    public WorkspaceNameSeparator Separator { get; init; } = WorkspaceNameSeparator.Hyphens;

    /// <summary>
    /// The letter case.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<WorkspaceNameCase>))]
    public WorkspaceNameCase Case { get; init; } = WorkspaceNameCase.Lowercase;

    /// <summary>
    /// Whether the name starts with the task's reference, e.g. <c>JIRA-123</c>.
    /// </summary>
    public bool IncludeTaskReference { get; init; } = true;

    /// <summary>
    /// Replaces values this version of Tammi doesn't know with the defaults.
    /// </summary>
    public WorkspaceNameFormat Normalize() => this with
    {
        Separator = Enum.IsDefined(Separator) ? Separator : WorkspaceNameSeparator.Hyphens,
        Case = Enum.IsDefined(Case) ? Case : WorkspaceNameCase.Lowercase,
    };
}
