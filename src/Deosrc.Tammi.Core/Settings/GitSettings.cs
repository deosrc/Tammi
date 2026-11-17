using System.Text.Json.Serialization;
using Deosrc.Tammi.Core.Catalogue;
using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// The Repositories section of the Settings screen, and the git graph's options (<see cref="GraphScope"/>, <see cref="GraphFileView"/>
/// and <see cref="SubjectLimit"/>). It keeps its earlier name, and its <c>git</c> key in
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
    /// The fewest characters <see cref="SubjectLimit"/> can be.
    /// </summary>
    public const int MinSubjectLimit = 1;

    /// <summary>
    /// The most characters <see cref="SubjectLimit"/> can be.
    /// </summary>
    public const int MaxSubjectLimit = 500;

    /// <summary>
    /// The <see cref="SubjectLimit"/> used until the user changes it.
    /// </summary>
    public const int DefaultSubjectLimit = 72;

    /// <summary>
    /// The fewest columns <see cref="DiffTabWidth"/> can be.
    /// </summary>
    public const int MinDiffTabWidth = 1;

    /// <summary>
    /// The most columns <see cref="DiffTabWidth"/> can be.
    /// </summary>
    public const int MaxDiffTabWidth = 16;

    /// <summary>
    /// The <see cref="DiffTabWidth"/> used until the user changes it: 6, a compromise between the usual 4 and 8.
    /// </summary>
    public const int DefaultDiffTabWidth = 6;

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
    /// Which commits a git graph shows when it opens: the one most recently chosen in a graph. Each graph can switch
    /// while it's open.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<GitGraphScope>))]
    public GitGraphScope GraphScope { get; init; } = GitGraphScope.WorkspaceBranch;

    /// <summary>
    /// How a git graph's details show the selected commit's changed files, as the user last chose in one.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<GitGraphFileView>))]
    public GitGraphFileView GraphFileView { get; init; } = GitGraphFileView.List;

    /// <summary>
    /// How file diffs treat whitespace changes, as the user last chose in one.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<GitDiffWhitespace>))]
    public GitDiffWhitespace DiffWhitespace { get; init; } = GitDiffWhitespace.Show;

    /// <summary>
    /// Whether file diffs ignore changes whose lines are all blank.
    /// </summary>
    public bool DiffIgnoreBlankLines { get; init; }

    /// <summary>
    /// How many columns a tab is drawn as in file diffs.
    /// </summary>
    public int DiffTabWidth { get; init; } = DefaultDiffTabWidth;

    /// <summary>
    /// How file diffs are laid out, as the user last chose in one.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<GitDiffLayout>))]
    public GitDiffLayout DiffLayout { get; init; } = GitDiffLayout.Unified;

    /// <summary>
    /// How many unchanged lines file diffs show around each change.
    /// </summary>
    public int DiffContextLines { get; init; } = GitDiffOptions.DefaultContextLines;

    /// <summary>
    /// How many characters a commit message's subject should have at most. It's a soft limit: the commit panel counts
    /// the characters left, and still commits a longer subject.
    /// </summary>
    public int SubjectLimit { get; init; } = DefaultSubjectLimit;

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
        SubjectLimit = SubjectLimit is >= MinSubjectLimit and <= MaxSubjectLimit ? SubjectLimit : DefaultSubjectLimit,
        DiffWhitespace = Enum.IsDefined(DiffWhitespace) ? DiffWhitespace : GitDiffWhitespace.Show,
        DiffTabWidth = DiffTabWidth is >= MinDiffTabWidth and <= MaxDiffTabWidth ? DiffTabWidth : DefaultDiffTabWidth,
        DiffLayout = Enum.IsDefined(DiffLayout) ? DiffLayout : GitDiffLayout.Unified,
        DiffContextLines = DiffContextLines is >= 0 and <= GitDiffOptions.MaxContextLines
            ? DiffContextLines
            : GitDiffOptions.DefaultContextLines,
    };

    /// <summary>
    /// What file diffs are computed with, as these settings say.
    /// </summary>
    public GitDiffOptions DiffOptions => new(DiffWhitespace, DiffIgnoreBlankLines, DiffContextLines);

    /// <summary>
    /// Whether <paramref name="other"/> holds the same settings, comparing the folder lists item by item.
    /// </summary>
    public bool Equals(GitSettings? other) =>
        other is not null
        && MonitoredFolders.SequenceEqual(other.MonitoredFolders)
        && LocalRepositories.SequenceEqual(other.LocalRepositories)
        && StatusRefreshInterval == other.StatusRefreshInterval
        && GraphScope == other.GraphScope
        && GraphFileView == other.GraphFileView
        && SubjectLimit == other.SubjectLimit
        && DiffWhitespace == other.DiffWhitespace
        && DiffIgnoreBlankLines == other.DiffIgnoreBlankLines
        && DiffTabWidth == other.DiffTabWidth
        && DiffLayout == other.DiffLayout
        && DiffContextLines == other.DiffContextLines;

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(
            MonitoredFolders.Count, LocalRepositories.Count, StatusRefreshInterval, GraphScope, GraphFileView, SubjectLimit)
        ^ HashCode.Combine(DiffWhitespace, DiffIgnoreBlankLines, DiffTabWidth, DiffLayout, DiffContextLines);
}
