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
    private readonly IGitWorkingTree _workingTree;
    private readonly IGitSync _sync;
    private readonly IGitTags _tags;
    private readonly IGitBranches _branches;
    private readonly IGitStash _stash;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<GitGraphRow> _rows = [];
    private IReadOnlyDictionary<string, IReadOnlyList<GitReference>> _labels =
        new Dictionary<string, IReadOnlyList<GitReference>>();
    private readonly Dictionary<string, string?> _anchors = [];
    private IGitHistoryCursor? _cursor;
    private GitGraphLayout _layout = new();
    private bool _loaded;
    private volatile bool _disposed;
    private int _syncing;

    internal WorkspaceGitGraph(
        Workspace workspace,
        WorkspaceRepository repository,
        string? submodulePath,
        GitGraphScope scope,
        IGitRepositoryReader reader,
        IGitWorkingTree workingTree,
        IGitSync sync,
        IGitTags tags,
        IGitBranches branches,
        IGitStash stash)
    {
        WorkspaceId = workspace.Id;
        Repository = repository;
        SubmodulePath = submodulePath;
        Scope = scope;
        _reader = reader;
        _workingTree = workingTree;
        _sync = sync;
        _tags = tags;
        _branches = branches;
        _stash = stash;
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
    /// The names of the tags the remote has, as last read. Empty until it's been read, or if there's no remote.
    /// </summary>
    public IReadOnlySet<string> RemoteTags { get; private set; } = new HashSet<string>();

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
    /// The selected stash, or <see langword="null"/> if none is. Its changed files are in <see cref="SelectedChanges"/>,
    /// and the details panel is then for the stash.
    /// </summary>
    public GitStashEntry? SelectedStash { get; private set; }

    /// <summary>
    /// Whether the working changes are selected, instead of a commit. The details panel is then for making a commit.
    /// </summary>
    public bool IsWorkingChangesSelected { get; private set; }

    /// <summary>
    /// The changed files in the working directory and index, as last read, or <see langword="null"/> before they're
    /// read or if they couldn't be.
    /// </summary>
    public IReadOnlyList<FileChange>? WorkingChanges { get; private set; }

    /// <summary>
    /// The files with changes in the working directory that aren't staged, in path order.
    /// </summary>
    public IReadOnlyList<FileChange> UnstagedChanges => [.. (WorkingChanges ?? []).Where(change => change.IsUnstaged || change.IsConflicted)];

    /// <summary>
    /// The files with staged changes, in path order.
    /// </summary>
    public IReadOnlyList<FileChange> StagedChanges => [.. (WorkingChanges ?? []).Where(change => change.IsStaged)];

    /// <summary>
    /// The sha of the commit the working changes are on top of, or <see langword="null"/> if there are no commits yet.
    /// </summary>
    public string? HeadSha { get; private set; }

    /// <summary>
    /// The draft message of the next commit, as last read or saved.
    /// </summary>
    public string Message { get; private set; } = "";

    /// <summary>
    /// Why the working changes couldn't be read, or why staging or committing failed, or <see langword="null"/>.
    /// </summary>
    public string? WorkingChangesProblem { get; private set; }

    /// <summary>
    /// The file diff that's shown in place of the graph, or <see langword="null"/> if the graph is shown.
    /// </summary>
    public FileDiff? ActiveDiff { get; private set; }

    /// <summary>
    /// Why the diff couldn't be read, or <see langword="null"/>. Set while <see cref="ActiveDiff"/> is.
    /// </summary>
    public string? DiffProblem { get; private set; }

    /// <summary>
    /// The file path and source of the diff shown, or <see langword="null"/> if the graph is shown.
    /// </summary>
    public (string Path, GitDiffSource Source)? DiffTarget { get; private set; }

    /// <summary>
    /// The fetch, pull or push that's running, or <see langword="null"/> when none is.
    /// </summary>
    public GitSyncOperation? Syncing { get; private set; }

    /// <summary>
    /// The last fetch, pull or push that failed, or <see langword="null"/> if the last one worked (or none has run).
    /// </summary>
    public GitSyncException? SyncProblem { get; private set; }

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
    /// Reads the first page, if the graph hasn't been read yet. Otherwise, as when its tab is shown again, reads it
    /// again if its refs have changed, and reads the working changes again, since files may have changed while the
    /// tab was hidden.
    /// </summary>
    public async Task LoadAsync()
    {
        if (!_loaded)
        {
            await RunAsync(() => Reload(0)).ConfigureAwait(false);
            return;
        }

        await RefreshIfChangedAsync().ConfigureAwait(false);
        await RefreshWorkingChangesAsync().ConfigureAwait(false);
    }

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
    public Task SelectAsync(string? sha) => RunAsync(() =>
    {
        ClearDiff();
        IsWorkingChangesSelected = false;
        SelectedStash = null;
        Select(sha);
    });

    /// <summary>
    /// Selects a stash, and reads the files it changed.
    /// </summary>
    public Task SelectStashAsync(GitStashEntry stash) => RunAsync(() =>
    {
        ClearDiff();
        IsWorkingChangesSelected = false;
        Selected = null;
        SelectStash(stash);
    });

    /// <summary>
    /// Selects the working changes, reads them and the saved commit message.
    /// </summary>
    public Task SelectWorkingChangesAsync() => RunAsync(() =>
    {
        ClearDiff();
        Selected = null;
        SelectedStash = null;
        SelectedChanges = null;
        SelectedChangesProblem = null;
        IsWorkingChangesSelected = true;
        ReadWorkingChanges(readMessage: true);
    });

    /// <summary>
    /// Reads the working changes again, e.g. after a file changed. Does nothing until the graph has been read.
    /// </summary>
    public Task RefreshWorkingChangesAsync() => RunAsync(() =>
    {
        if (_loaded)
        {
            ReadWorkingChanges(readMessage: false);
        }
    });

    /// <summary>
    /// Stages the changes to files.
    /// </summary>
    public Task StageAsync(IReadOnlyCollection<FileChange> files) =>
        ChangeIndexAsync(token => _workingTree.StageAsync(Path, PathsOf(files), token));

    /// <summary>
    /// Unstages the changes to files.
    /// </summary>
    public Task UnstageAsync(IReadOnlyCollection<FileChange> files) =>
        ChangeIndexAsync(token => _workingTree.UnstageAsync(Path, PathsOf(files), token));

    /// <summary>
    /// Discards the unstaged changes to files: restores them from the index, and deletes the untracked ones. The
    /// changes can't be brought back.
    /// </summary>
    public Task DiscardAsync(IReadOnlyCollection<FileChange> files) =>
        ChangeIndexAsync(token => _workingTree.DiscardAsync(
            Path,
            PathsOf(files.Where(file => !file.IsUntracked)),
            PathsOf(files.Where(file => file.IsUntracked)),
            token));

    /// <summary>
    /// Discards every unstaged change in the working directory. Staged changes are kept.
    /// </summary>
    public Task DiscardAllAsync() => ChangeIndexAsync(token => _workingTree.DiscardAllAsync(Path, token));

    /// <summary>
    /// What the user has asked to discard, and has yet to confirm, or <see langword="null"/>.
    /// </summary>
    public DiscardRequest? PendingDiscard { get; private set; }

    /// <summary>
    /// Asks the user to confirm discarding unstaged changes, which can't be undone.
    /// </summary>
    /// <param name="files">The files to discard, or <see langword="null"/> for every unstaged change.</param>
    public void RequestDiscard(IReadOnlyCollection<FileChange>? files)
    {
        PendingDiscard = new DiscardRequest(files is null ? null : [.. files]);
        Changed?.Invoke();
    }

    /// <summary>
    /// Discards what was asked for, once the user has confirmed.
    /// </summary>
    public async Task ConfirmDiscardAsync()
    {
        DiscardRequest? request = PendingDiscard;
        PendingDiscard = null;
        if (request is null)
        {
            return;
        }

        await (request.Files is null ? DiscardAllAsync() : DiscardAsync(request.Files)).ConfigureAwait(false);
    }

    /// <summary>
    /// Forgets the request to discard changes.
    /// </summary>
    public void CancelDiscard()
    {
        if (PendingDiscard is not null)
        {
            PendingDiscard = null;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// What the user has asked to reset the current branch to, and has yet to confirm, or <see langword="null"/>.
    /// </summary>
    public ResetRequest? PendingReset { get; private set; }

    /// <summary>
    /// Asks the user to confirm resetting the current branch to a commit.
    /// </summary>
    public void RequestReset(string sha, GitResetMode mode)
    {
        PendingReset = new ResetRequest(sha, mode);
        Changed?.Invoke();
    }

    /// <summary>
    /// Resets the current branch as was asked for, once the user has confirmed.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="WorkingChangesProblem"/> says why.</returns>
    public async Task<bool> ConfirmResetAsync()
    {
        ResetRequest? request = PendingReset;
        PendingReset = null;
        return request is not null && await ResetAsync(request.Sha, request.Mode).ConfigureAwait(false);
    }

    /// <summary>
    /// Forgets the request to reset.
    /// </summary>
    public void CancelReset()
    {
        if (PendingReset is not null)
        {
            PendingReset = null;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Moves the current branch to a commit, and resets the index and working directory as the mode says. Then reads
    /// the graph again.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="WorkingChangesProblem"/> says why.</returns>
    public async Task<bool> ResetAsync(string sha, GitResetMode mode)
    {
        bool worked = false;
        try
        {
            await _workingTree.ResetAsync(Path, sha, mode).ConfigureAwait(false);
            WorkingChangesProblem = null;
            worked = true;
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            WorkingChangesProblem = ex.Message;
        }

        // The branch, index and files may have changed even when it failed part way.
        await RunAsync(() => Reload(Rows.Count)).ConfigureAwait(false);
        return worked;
    }

    /// <summary>
    /// Saves the draft of the next commit's message.
    /// </summary>
    public async Task SaveMessageAsync(string message)
    {
        Message = message;
        try
        {
            await _workingTree.WriteMessageAsync(Path, message).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            WorkingChangesProblem = ex.Message;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Commits what's staged, then shows the new commit.
    /// </summary>
    /// <returns>Whether the commit was made. If not, <see cref="WorkingChangesProblem"/> says why.</returns>
    public async Task<bool> CommitAsync(string message)
    {
        bool committed = false;
        try
        {
            await _workingTree.CommitAsync(Path, message).ConfigureAwait(false);
            committed = true;
            Message = "";
            WorkingChangesProblem = null;
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            WorkingChangesProblem = ex.Message;
        }

        if (committed)
        {
            await RunAsync(() =>
            {
                Reload(0);
                IsWorkingChangesSelected = true;
                ReadWorkingChanges(readMessage: false);
            }).ConfigureAwait(false);
        }
        else
        {
            await RefreshWorkingChangesAsync().ConfigureAwait(false);
        }

        return committed;
    }

    /// <summary>
    /// Fetches, pulls or pushes, then reads the graph again so the new commits and branches show. Does nothing while
    /// another of them is running. <see cref="Syncing"/> follows it, and a failure is
    /// left in <see cref="SyncProblem"/>.
    /// </summary>
    /// <returns>Whether it worked.</returns>
    public async Task<bool> SyncAsync(GitSyncOperation operation)
    {
        if (_disposed || Interlocked.CompareExchange(ref _syncing, 1, 0) == 1)
        {
            return false;
        }

        Syncing = operation;
        SyncProblem = null;
        Changed?.Invoke();

        bool worked = false;
        try
        {
            Task task = operation switch
            {
                GitSyncOperation.Fetch => _sync.FetchAsync(Path),
                GitSyncOperation.Pull => _sync.PullAsync(Path),
                _ => _sync.PushAsync(Path),
            };
            await task.ConfigureAwait(false);
            worked = true;
        }
        catch (GitSyncException ex)
        {
            SyncProblem = ex;
        }
        catch (GitException ex)
        {
            SyncProblem = new GitSyncException(operation, GitSyncFailure.Other, ex.Message, "", ex);
        }
        finally
        {
            Syncing = null;
            Volatile.Write(ref _syncing, 0);
        }

        // The refs, and after a pull or a conflicted one the working changes, have changed even when it failed.
        await RunAsync(() => Reload(Rows.Count)).ConfigureAwait(false);
        return worked;
    }

    /// <summary>
    /// The last tag action that failed, or <see langword="null"/> if the last one worked (or none has run).
    /// </summary>
    public string? TagProblem { get; private set; }

    /// <summary>
    /// Creates a lightweight tag on a commit, then reads the graph again so it shows.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="TagProblem"/> says why.</returns>
    public Task<bool> CreateTagAsync(string name, string sha) =>
        ChangeTagsAsync(token => _tags.CreateAsync(Path, name, sha, token));

    /// <summary>
    /// Pushes a tag to the remote.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="TagProblem"/> says why.</returns>
    public Task<bool> PushTagAsync(string name) => ChangeTagsAsync(token => _tags.PushAsync(Path, name, token));

    /// <summary>
    /// Deletes a tag locally and, if the remote has it, there too.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="TagProblem"/> says why.</returns>
    public Task<bool> DeleteTagAsync(string name) => ChangeTagsAsync(token => _tags.DeleteAsync(Path, name, token));

    /// <summary>
    /// Finds the remote that tags are pushed to, or <see langword="null"/> if there's none.
    /// </summary>
    public async Task<string?> TagRemoteAsync()
    {
        try
        {
            return await _tags.GetRemoteAsync(Path).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Finds out whether the remote has a tag. Contacts the remote, so it can be slow.
    /// </summary>
    public async Task<bool> TagExistsOnRemoteAsync(string name)
    {
        try
        {
            return await _tags.ExistsOnRemoteAsync(Path, name).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Clears <see cref="TagProblem"/>, once the user has seen it.
    /// </summary>
    public void DismissTagProblem()
    {
        if (TagProblem is not null)
        {
            TagProblem = null;
            Changed?.Invoke();
        }
    }

    // Asks the remote which tags it has, without holding up the graph, then redraws so their badges show it.
    private async Task RefreshRemoteTagsAsync()
    {
        try
        {
            IReadOnlySet<string> found = await _tags.ListRemoteAsync(Path).ConfigureAwait(false);
            RemoteTags = found;
            Changed?.Invoke();
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            // The badges just don't show the remote icon.
        }
    }

    private async Task<bool> ChangeTagsAsync(Func<CancellationToken, Task> change)
    {
        bool worked = false;
        try
        {
            await change(CancellationToken.None).ConfigureAwait(false);
            TagProblem = null;
            worked = true;
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            TagProblem = ex.Message;
        }

        // Tags may have changed even when it failed part way.
        await RunAsync(() => Reload(Rows.Count)).ConfigureAwait(false);
        return worked;
    }

    /// <summary>
    /// The last branch action that failed, or <see langword="null"/> if the last one worked (or none has run).
    /// </summary>
    public string? BranchProblem { get; private set; }

    /// <summary>
    /// Creates a local branch on a commit, and optionally checks it out.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="BranchProblem"/> says why.</returns>
    public Task<bool> CreateBranchAsync(string name, string sha, bool checkout) =>
        ChangeBranchesAsync(token => _branches.CreateAsync(Path, name, sha, checkout, token));

    /// <summary>
    /// Checks out a local branch, or a local branch that tracks a remote-tracking branch.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="BranchProblem"/> says why.</returns>
    public Task<bool> CheckoutBranchAsync(string name) => ChangeBranchesAsync(token => _branches.CheckoutAsync(Path, name, token));

    /// <summary>
    /// Renames a local branch. Its remote branch isn't renamed.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="BranchProblem"/> says why.</returns>
    public Task<bool> RenameBranchAsync(string name, string newName) =>
        ChangeBranchesAsync(token => _branches.RenameAsync(Path, name, newName, token));

    /// <summary>
    /// Deletes a local branch, the remote branch it tracks, or both.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="BranchProblem"/> says why.</returns>
    public Task<bool> DeleteBranchAsync(string name, bool deleteLocal, bool deleteRemote) =>
        ChangeBranchesAsync(token => _branches.DeleteAsync(Path, name, deleteLocal, deleteRemote, token));

    /// <summary>
    /// Deletes a remote branch, given as a remote-tracking name such as <c>origin/feature</c>.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="BranchProblem"/> says why.</returns>
    public Task<bool> DeleteRemoteBranchAsync(string remoteBranch) =>
        ChangeBranchesAsync(token => _branches.DeleteRemoteAsync(Path, remoteBranch, token));

    /// <summary>
    /// Finds the remote branch a local branch tracks, or <see langword="null"/> if it has none.
    /// </summary>
    public async Task<GitUpstream?> BranchUpstreamAsync(string name)
    {
        try
        {
            return await _branches.GetUpstreamAsync(Path, name).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether a local branch is the workspace's branch, which can't be renamed or deleted from here.
    /// </summary>
    public bool IsWorkspaceBranch(string name) => SubmodulePath is null && name == Repository.Branch;

    /// <summary>
    /// Clears <see cref="BranchProblem"/>, once the user has seen it.
    /// </summary>
    public void DismissBranchProblem()
    {
        if (BranchProblem is not null)
        {
            BranchProblem = null;
            Changed?.Invoke();
        }
    }

    /// The stashes, newest first, as last read.
    /// </summary>
    public IReadOnlyList<GitStashEntry> Stashes { get; private set; } = [];

    /// <summary>
    /// Why the last stash, apply, pop or drop failed, or <see langword="null"/> if the last one worked (or none has run).
    /// </summary>
    public string? StashProblem { get; private set; }

    /// <summary>
    /// Stashes changes, including new files.
    /// </summary>
    /// <param name="files">The files to stash, or <see langword="null"/> to stash every change.</param>
    /// <param name="message">What to call the stash, or <see langword="null"/> for git's own.</param>
    /// <returns>Whether it worked. If not, <see cref="StashProblem"/> says why.</returns>
    public Task<bool> StashAsync(IReadOnlyCollection<FileChange>? files = null, string? message = null) =>
        ChangeStashAsync(token => _stash.PushAsync(Path, files is null ? null : PathsOf(files), message, token));

    /// <summary>
    /// Applies a stash to the working directory and keeps it.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="StashProblem"/> says why.</returns>
    public Task<bool> ApplyStashAsync(GitStashEntry stash) =>
        ChangeStashAsync(async token => { await _stash.ApplyAsync(Path, stash.Index, token).ConfigureAwait(false); return true; });

    /// <summary>
    /// Applies a stash to the working directory and drops it. A stash that conflicts is kept.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="StashProblem"/> says why.</returns>
    public Task<bool> PopStashAsync(GitStashEntry stash) =>
        ChangeStashAsync(async token => { await _stash.PopAsync(Path, stash.Index, token).ConfigureAwait(false); return true; });

    /// <summary>
    /// Deletes a stash without applying it.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="StashProblem"/> says why.</returns>
    public Task<bool> DropStashAsync(GitStashEntry stash) =>
        ChangeStashAsync(async token => { await _stash.DropAsync(Path, stash.Index, token).ConfigureAwait(false); return true; });

    /// <summary>
    /// The stash the user has asked to drop, and has yet to confirm, or <see langword="null"/>.
    /// </summary>
    public GitStashEntry? DropRequest { get; private set; }

    /// <summary>
    /// Asks the user to confirm dropping a stash, which can't be undone.
    /// </summary>
    public void RequestDropStash(GitStashEntry stash)
    {
        DropRequest = stash;
        Changed?.Invoke();
    }

    /// <summary>
    /// Drops the stash that was asked for, once the user has confirmed.
    /// </summary>
    /// <returns>Whether it worked. If not, <see cref="StashProblem"/> says why.</returns>
    public async Task<bool> ConfirmDropStashAsync()
    {
        GitStashEntry? stash = DropRequest;
        DropRequest = null;
        return stash is not null && await DropStashAsync(stash).ConfigureAwait(false);
    }

    /// <summary>
    /// Forgets the request to drop a stash.
    /// </summary>
    public void CancelDropStash()
    {
        if (DropRequest is not null)
        {
            DropRequest = null;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Clears <see cref="StashProblem"/>, once the user has seen it.
    /// </summary>
    public void DismissStashProblem()
    {
        if (StashProblem is not null)
        {
            StashProblem = null;
            Changed?.Invoke();
        }
    }

    private async Task<bool> ChangeBranchesAsync(Func<CancellationToken, Task> change)
    {
        bool worked = false;
        try
        {
            await change(CancellationToken.None).ConfigureAwait(false);
            BranchProblem = null;
            worked = true;
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            BranchProblem = ex.Message;
        }

        // Branches (and, after a checkout, the working files) may have changed even when it failed part way.
        await RunAsync(() =>
        {
            Reload(Rows.Count);
        }).ConfigureAwait(false);
        return worked;
    }

    private async Task<bool> ChangeStashAsync(Func<CancellationToken, Task<bool>> change)
    {
        bool worked = false;
        try
        {
            worked = await change(CancellationToken.None).ConfigureAwait(false);
            StashProblem = worked ? null : "There was nothing to stash.";
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            StashProblem = ex.Message;
        }

        // A conflicted apply or pop changes the working directory even though it failed.
        await RunAsync(() => ReadWorkingChanges(readMessage: false)).ConfigureAwait(false);
        return worked;
    }

    /// <summary>
    /// Clears <see cref="SyncProblem"/>, once the user has seen it.
    /// </summary>
    public void DismissSyncProblem()
    {
        if (SyncProblem is not null)
        {
            SyncProblem = null;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Shows the diff of a file in place of the graph.
    /// </summary>
    /// <param name="source">Which two states to compare.</param>
    /// <param name="file">The file's path.</param>
    /// <param name="oldPath">For a rename, the path the file was renamed from.</param>
    /// <param name="sha">For <see cref="GitDiffSource.Commit"/>, the commit.</param>
    public Task OpenDiffAsync(GitDiffSource source, string file, string? oldPath = null, string? sha = null) => RunAsync(() =>
    {
        DiffTarget = (file, source);
        ActiveDiff = null;
        DiffProblem = null;
        Changed?.Invoke();
        try
        {
            ActiveDiff = _reader.ReadDiff(Path, source, file, oldPath, sha);
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            DiffProblem = ex.Message;
        }
    });

    /// <summary>
    /// Shows the graph again.
    /// </summary>
    public void CloseDiff()
    {
        ClearDiff();
        Changed?.Invoke();
    }

    private void ClearDiff()
    {
        DiffTarget = null;
        ActiveDiff = null;
        DiffProblem = null;
    }

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

    private static string[] PathsOf(IEnumerable<FileChange> files) =>
        [.. files.SelectMany(file => file.OldPath is null ? [file.Path] : new[] { file.Path, file.OldPath }).Distinct()];

    private async Task ChangeIndexAsync(Func<CancellationToken, Task> change)
    {
        try
        {
            await change(CancellationToken.None).ConfigureAwait(false);
            WorkingChangesProblem = null;
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            WorkingChangesProblem = ex.Message;
        }

        await RefreshWorkingChangesAsync().ConfigureAwait(false);
    }

    private void ReadWorkingChanges(bool readMessage)
    {
        try
        {
            Deosrc.Tammi.Core.Git.RepositoryStatus status = _reader.ReadStatus(Path);
            // A submodule on a different commit from the recorded one is a change to stage and commit too.
            WorkingChanges =
            [
                .. status.Changes
                    .Concat(status.Submodules.Select(FileChange.ForSubmodule).OfType<FileChange>())
                    .OrderBy(change => change.Path, StringComparer.Ordinal),
            ];
            HeadSha = status.HeadSha;
            IReadOnlyList<GitStashEntry> stashes = AnchorStashes(_stash.ListAsync(Path).GetAwaiter().GetResult());
            if (!stashes.SequenceEqual(Stashes))
            {
                // Only replaced when they've changed, so the view can tell by reference.
                Stashes = stashes;
            }

            if (SelectedStash is { } selectedStash)
            {
                // Follow the selected stash as the others come and go and its index changes, or let go of it when it's gone.
                GitStashEntry? current = Stashes.FirstOrDefault(stash => stash.Sha == selectedStash.Sha);
                if (current is null)
                {
                    ClearDiff();
                    SelectedStash = null;
                    SelectedChanges = null;
                    SelectedChangesProblem = null;
                }
                else
                {
                    SelectedStash = current;
                }
            }

            if (IsWorkingChangesSelected && WorkingChanges.Count == 0)
            {
                // The row is gone (e.g. after a commit), so show HEAD's commit instead.
                IsWorkingChangesSelected = false;
                Select(HeadSha);
            }

            if (readMessage)
            {
                Message = _workingTree.ReadMessageAsync(Path).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex) when (ex is GitException or LibGit2SharpException or IOException or UnauthorizedAccessException)
        {
            WorkingChanges = null;
            WorkingChangesProblem = ex.Message;
        }

        Changed?.Invoke();
    }

    // Stashes made on a commit the graph doesn't show are drawn on the nearest commit it does: where that commit's
    // branch joins HEAD's history.
    private IReadOnlyList<GitStashEntry> AnchorStashes(IReadOnlyList<GitStashEntry> stashes)
    {
        var shown = _rows.Select(row => row.Commit.Sha).ToHashSet();
        var anchored = new List<GitStashEntry>();
        foreach (GitStashEntry stash in stashes)
        {
            string? anchor = stash.BaseSha;
            if (stash.BaseSha.Length > 0 && !shown.Contains(stash.BaseSha))
            {
                if (!_anchors.TryGetValue(stash.BaseSha, out anchor))
                {
                    anchor = _stash.FindMergeBaseWithHeadAsync(Path, stash.BaseSha).GetAwaiter().GetResult();
                    _anchors[stash.BaseSha] = anchor;
                }
            }

            anchored.Add(stash with { AnchorSha = anchor });
        }

        return anchored;
    }

    // Opens the history again with the current scope and reads at least `count` commits.
    private void Reload(int count)
    {
        string? selected = Selected?.Commit.Sha;
        string? selectedStash = SelectedStash?.Sha;
        CloseCursor();
        _rows.Clear();
        _anchors.Clear();
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
            if (Refs.References.Any(reference => reference.Kind == GitReferenceKind.Tag))
            {
                _ = RefreshRemoteTagsAsync();
            }
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
        ReadWorkingChanges(readMessage: false);
        if (selected is not null)
        {
            Select(selected);
        }
        else if (Stashes.FirstOrDefault(stash => stash.Sha == selectedStash) is { } stillThere)
        {
            SelectStash(stillThere);
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

    private void SelectStash(GitStashEntry stash)
    {
        SelectedStash = stash;
        SelectedChanges = null;
        SelectedChangesProblem = null;
        Changed?.Invoke();
        try
        {
            SelectedChanges = _reader.ReadCommitChanges(Path, stash.Sha);
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            SelectedChangesProblem = ex.Message;
        }
    }

    private void Select(string? sha)
    {
        SelectedStash = null;
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
            StartingPoints = ["HEAD", Repository.Branch, Repository.MergeTarget],
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
