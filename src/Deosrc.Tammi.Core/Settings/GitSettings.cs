using System.Text.Json.Serialization;
using Deosrc.Tammi.Core.Catalogue;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// The Repositories section of the Settings screen, and the git graph's options (<see cref="GraphScope"/> on the Git
/// section, and <see cref="GraphFileView"/>). It keeps its earlier name, and its <c>git</c> key in
/// <c>settings.json</c>, from when the Repositories section was called Git.
/// </summary>
public sealed record GitSettings
{
    /// <summary>
    /// The shortest <see cref="StatusRefreshInterval"/>.
    /// </summary>
    public static readonly TimeSpan MinStatusRefreshInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The longest <see cref="StatusRefreshInterval"/>.
    /// </summary>
    public static readonly TimeSpan MaxStatusRefreshInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// The <see cref="StatusRefreshInterval"/> used until the user changes it.
    /// </summary>
    public static readonly TimeSpan DefaultStatusRefreshInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The Monitored Folders: local folders scanned for git repositories to add to the Repository Catalogue (see
    /// <see cref="RepositoryScanner"/>).
    /// </summary>
    public IReadOnlyList<string> MonitoredFolders { get; init; } = [];

    /// <summary>
    /// The manually added local repositories: the working directories of repositories added to the Repository
    /// Catalogue one by one, wherever they are.
    /// </summary>
    public IReadOnlyList<string> LocalRepositories { get; init; } = [];

    /// <summary>
    /// How often the git status of workspaces that aren't open in a tab is refreshed.
    /// </summary>
    public TimeSpan StatusRefreshInterval { get; init; } = DefaultStatusRefreshInterval;

    /// <summary>
    /// Which commits a git graph shows when it opens. Each graph can switch while it's open.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<GitGraphScope>))]
    public GitGraphScope GraphScope { get; init; } = GitGraphScope.WorkspaceBranch;

    /// <summary>
    /// How a git graph's details show the selected commit's changed files, as the user last chose in one.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<GitGraphFileView>))]
    public GitGraphFileView GraphFileView { get; init; } = GitGraphFileView.List;

    /// <summary>
    /// Replaces out-of-range values, and values this version of Tammi doesn't know, with valid ones, and writes each
    /// folder as a full path, once.
    /// </summary>
    public GitSettings Normalize() => this with
    {
        MonitoredFolders = FileSystemPaths.NormalizeAll(MonitoredFolders),
        LocalRepositories = FileSystemPaths.NormalizeAll(LocalRepositories),
        StatusRefreshInterval = StatusRefreshInterval < MinStatusRefreshInterval
            ? MinStatusRefreshInterval
            : StatusRefreshInterval > MaxStatusRefreshInterval
                ? MaxStatusRefreshInterval
                : StatusRefreshInterval,
        GraphScope = Enum.IsDefined(GraphScope) ? GraphScope : GitGraphScope.WorkspaceBranch,
        GraphFileView = Enum.IsDefined(GraphFileView) ? GraphFileView : GitGraphFileView.List,
    };

    /// <summary>
    /// Whether <paramref name="other"/> holds the same settings, comparing the folder lists item by item.
    /// </summary>
    public bool Equals(GitSettings? other) =>
        other is not null
        && MonitoredFolders.SequenceEqual(other.MonitoredFolders)
        && LocalRepositories.SequenceEqual(other.LocalRepositories)
        && StatusRefreshInterval == other.StatusRefreshInterval
        && GraphScope == other.GraphScope
        && GraphFileView == other.GraphFileView;

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(MonitoredFolders.Count, LocalRepositories.Count, StatusRefreshInterval, GraphScope, GraphFileView);
}
