using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Settings;
using LibGit2Sharp;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The git graph of one of a workspace's repositories, or of one of its submodules, shown as a main area tab. Open
/// and close it with <see cref="WorkspaceGitGraphs"/>.
/// </summary>
/// <remarks>
/// <para>
/// The history is read a page at a time as the user scrolls, and laid out by <see cref="GitGraphLayout"/>. While the
/// graph is open it keeps its repository open, so pages after the first don't walk the history again.
/// </para>
/// <para>
/// Its methods read on a background thread, one at a time, and raise <see cref="Changed"/> when they're done.
/// </para>
/// </remarks>
public sealed class WorkspaceGitGraph : IMainAreaTab, IDisposable
{
    /// <summary>
    /// How many commits are read at a time.
    /// </summary>
    public const int PageSize = 500;

    private readonly IGitRepositoryReader _reader;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<GitGraphRow> _rows = [];
    private IReadOnlyDictionary<string, IReadOnlyList<GitReference>> _labels =
        new Dictionary<string, IReadOnlyList<GitReference>>();
    private IGitHistoryCursor? _cursor;
    private GitGraphLayout _layout = new();
    private bool _loaded;
    private volatile bool _disposed;

    internal WorkspaceGitGraph(
        Workspace workspace,
        WorkspaceRepository repository,
        string? submodulePath,
        GitGraphScope scope,
        IGitRepositoryReader reader)
    {
        WorkspaceId = workspace.Id;
        Repository = repository;
        SubmodulePath = submodulePath;
        Scope = scope;
        _reader = reader;
        string worktree = workspace.GetWorktreePath(repository);
        Path = submodulePath is null
            ? worktree
            : System.IO.Path.Combine(worktree, submodulePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Name = NameFor(repository, submodulePath);
        StartOrder = MainAreaTabOrder.Next();
    }

    /// <inheritdoc />
    public string WorkspaceId { get; }

    /// <summary>
    /// The tab's name: <c>Git: </c> and the repository's folder, followed by the submodule's path for a submodule,
    /// e.g. <c>Git: api</c> or <c>Git: api/libs/core</c>.
    /// </summary>
    public string Name { get; }

    /// <inheritdoc />
    public long StartOrder { get; }

    /// <summary>
    /// The workspace's repository the graph is of, or holds the submodule it's of.
    /// </summary>
    public WorkspaceRepository Repository { get; }

    /// <summary>
    /// The submodule's path in <see cref="Repository"/>, with <c>/</c> separators, or <see langword="null"/> for the
    /// repository itself.
    /// </summary>
    public string? SubmodulePath { get; }

    /// <summary>
    /// The working directory read: the Workspace Worktree, or the submodule's folder in it.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Which commits the graph shows.
    /// </summary>
    public GitGraphScope Scope { get; private set; }

    /// <summary>
    /// The rows read so far, newest first.
    /// </summary>
    public IReadOnlyList<GitGraphRow> Rows { get; private set; } = [];

    /// <summary>
    /// The repository's HEAD, branches and tags, as last read.
    /// </summary>
    public GitRefs Refs { get; private set; } = GitRefs.Empty;

    /// <summary>
    /// The most lanes any row read so far uses.
    /// </summary>
    public int LaneCount { get; private set; }

    /// <summary>
    /// Whether every commit in the graph has been read.
    /// </summary>
    public bool IsComplete { get; private set; }

    /// <summary>
    /// Whether the graph is being read.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Why the graph couldn't be read, or <see langword="null"/> if it could (or hasn't been read yet).
    /// </summary>
    public string? Problem { get; private set; }

    /// <summary>
    /// The selected commit's row, or <see langword="null"/> if none is selected.
    /// </summary>
    public GitGraphRow? Selected { get; private set; }

    /// <summary>
    /// The files the selected commit changed, or <see langword="null"/> while they're read or if they couldn't be.
    /// </summary>
    public IReadOnlyList<CommitFileChange>? SelectedChanges { get; private set; }

    /// <summary>
    /// Why the selected commit's changed files couldn't be read, or <see langword="null"/>.
    /// </summary>
    public string? SelectedChangesProblem { get; private set; }

    /// <summary>
    /// Raised when the graph has been read, or the selection has changed. It can be raised on a background thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The name of the graph tab for a repository, or one of its submodules.
    /// </summary>
    public static string NameFor(WorkspaceRepository repository, string? submodulePath)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return submodulePath is null ? $"Git: {repository.FolderName}" : $"Git: {repository.FolderName}/{submodulePath}";
    }

    /// <summary>
    /// The branches and tags that point to a commit, in the order of <see cref="GitRefs.References"/>.
    /// </summary>
    public IReadOnlyList<GitReference> LabelsFor(string sha) => _labels.GetValueOrDefault(sha) ?? [];

    /// <summary>
    /// Reads the first page, if the graph hasn't been read yet. Otherwise reads it again if its refs have changed.
    /// </summary>
    public Task LoadAsync() => _loaded ? RefreshIfChangedAsync() : RunAsync(() => Reload(0));

    /// <summary>
    /// Reads the next page, unless every commit has been read.
    /// </summary>
    public Task LoadMoreAsync() => RunAsync(() =>
    {
        if (_cursor is not null && !IsComplete)
        {
            ReadPage(PageSize);
        }
    });

    /// <summary>
    /// Shows other commits, reading the graph again from the top.
    /// </summary>
    public Task SetScopeAsync(GitGraphScope scope) => RunAsync(() =>
    {
        if (scope != Scope)
        {
            Scope = scope;
            Reload(0);
        }
    });

    /// <summary>
    /// Reads the graph again from the top, as many commits as were read before, keeping the selection if it's still
    /// in the graph.
    /// </summary>
    public Task RefreshAsync() => RunAsync(() => Reload(Rows.Count));

    /// <summary>
    /// Reads the refs again and, only if they've changed (e.g. after a commit, fetch or checkout), the graph too.
    /// Does nothing until the graph has been read.
    /// </summary>
    public Task RefreshIfChangedAsync() => RunAsync(() =>
    {
        if (!_loaded)
        {
            return;
        }

        if (Problem is not null || !RefsUnchanged())
        {
            Reload(Rows.Count);
        }
    });

    /// <summary>
    /// Selects a commit that's been read, and reads the files it changed. <see langword="null"/>, or a commit that
    /// hasn't been read, clears the selection.
    /// </summary>
    public Task SelectAsync(string? sha) => RunAsync(() => Select(sha));

    /// <summary>
    /// Closes the repository. A read that's under way finishes first, and then closes it. Reads asked for afterwards do
    /// nothing.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_gate.Wait(0))
        {
            CloseCursor();
            _gate.Release();
        }
    }

    // Opens the history again with the current scope and reads at least `count` commits.
    private void Reload(int count)
    {
        string? selected = Selected?.Commit.Sha;
        CloseCursor();
        _rows.Clear();
        _layout = new GitGraphLayout();
        LaneCount = 0;
        IsComplete = false;
        Selected = null;
        SelectedChanges = null;
        SelectedChangesProblem = null;
        _loaded = true;

        try
        {
            Refs = _reader.ReadRefs(Path);
            _labels = Refs.References
                .GroupBy(reference => reference.TargetSha)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<GitReference>)group.ToArray());
            _cursor = _reader.OpenHistory(Path, OptionsFor(Scope));
            Problem = null;
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            Refs = GitRefs.Empty;
            _labels = new Dictionary<string, IReadOnlyList<GitReference>>();
            Problem = ex.Message;
            IsComplete = true;
            Publish();
            return;
        }

        ReadPage(Math.Max(count, PageSize));
        if (selected is not null)
        {
            Select(selected);
        }
    }

    private void ReadPage(int count)
    {
        try
        {
            IReadOnlyList<CommitInfo> commits = _cursor!.ReadNext(count);
            foreach (GitGraphRow row in _layout.Append(commits))
            {
                _rows.Add(row);
                LaneCount = Math.Max(LaneCount, row.LaneCount);
            }

            IsComplete = _cursor.IsComplete;
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            Problem = ex.Message;
            IsComplete = true;
        }

        Publish();
    }

    private void Select(string? sha)
    {
        Selected = sha is null ? null : _rows.Find(row => row.Commit.Sha == sha);
        SelectedChanges = null;
        SelectedChangesProblem = null;
        Changed?.Invoke();
        if (Selected is null)
        {
            return;
        }

        try
        {
            SelectedChanges = _reader.ReadCommitChanges(Path, Selected.Commit.Sha);
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            SelectedChangesProblem = ex.Message;
        }
    }

    // Whether the refs read now are the ones the graph shows. Refs that can't be read count as changed, so reading the
    // graph again says why.
    private bool RefsUnchanged()
    {
        try
        {
            return _reader.ReadRefs(Path).Equals(Refs);
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            return false;
        }
    }

    private HistoryOptions OptionsFor(GitGraphScope scope) => scope switch
    {
        GitGraphScope.Everything => new HistoryOptions { AllRefs = true },
        _ when SubmodulePath is not null => new HistoryOptions { StartingPoints = ["HEAD"], IncludeUpstreams = true },
        _ => new HistoryOptions
        {
            StartingPoints = ["HEAD", Repository.Branch, Repository.BaseBranch],
            IncludeUpstreams = true,
        },
    };

    // Runs an operation on a background thread, after the one before it, and raises Changed when it's done. Once the
    // graph is closed, it does nothing, since the view may still ask for a page as it goes.
    private async Task RunAsync(Action operation)
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            IsLoading = true;
            await Task.Run(operation).ConfigureAwait(false);
        }
        finally
        {
            IsLoading = false;
            if (_disposed)
            {
                CloseCursor();
            }

            _gate.Release();
        }

        if (!_disposed)
        {
            Changed?.Invoke();
        }
    }

    // Shares what's been read with the UI. The row list is copied, so a later page doesn't change it as it's shown.
    private void Publish() => Rows = [.. _rows];

    private void CloseCursor()
    {
        _cursor?.Dispose();
        _cursor = null;
    }

    private static bool IsReadError(Exception ex) =>
        ex is GitException or LibGit2SharpException or IOException or UnauthorizedAccessException;
}
