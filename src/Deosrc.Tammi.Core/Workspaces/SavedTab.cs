using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A tab that was open, kept so it can be opened again after Tammi restarts. Every tab control keeps its tabs in this
/// same shape: the tabs in tab order and the one that was shown, each of which can hold tabs of its own. A tab that
/// isn't kept (like a terminal) isn't listed, and shown as nothing.
/// </summary>
/// <param name="Kind">One of the kinds below.</param>
/// <param name="Id">
/// What identifies the tab among its kind: a workspace's ID, an agent's Agent Session ID, or the folder name of a git
/// graph's repository in a workspace.
/// </param>
/// <param name="Path">
/// A repository's path, a git graph's working directory (in a repository's tab), or a submodule's path in its
/// repository (in a workspace's tab).
/// </param>
/// <param name="Tabs">The tab's own tabs, in tab order, or <c>null</c> if it has none.</param>
/// <param name="Active">The one of <paramref name="Tabs"/> that was shown, or <c>null</c> for the default.</param>
public sealed record SavedTab(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Id = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Path = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SavedTab>? Tabs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SavedTab? Active = null)
{
    /// <summary>
    /// The kind of a workspace's tab in the main tab strip.
    /// </summary>
    public const string WorkspaceKind = "workspace";

    /// <summary>
    /// The kind of a repository's own tab in the main tab strip.
    /// </summary>
    public const string RepositoryKind = "repository";

    /// <summary>
    /// The kind of an agent tab.
    /// </summary>
    public const string AgentKind = "agent";

    /// <summary>
    /// The kind of a git graph tab.
    /// </summary>
    public const string GitGraphKind = "git";

    /// <summary>
    /// Whether another tab is equal: the same tab, with the same tabs in the same order.
    /// </summary>
    public bool Equals(SavedTab? other) =>
        other is not null
        && IsSameTab(other)
        && Equals(Active, other.Active)
        && (Tabs ?? []).SequenceEqual(other.Tabs ?? []);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Id, Path, Active, Tabs?.Count ?? 0);

    /// <summary>
    /// Whether another tab is the same tab: it has the same kind, ID and path, whatever its own tabs.
    /// </summary>
    public bool IsSameTab(SavedTab? other) =>
        other is not null && Kind == other.Kind && Id == other.Id && Path == other.Path;
}
