using LibGit2Sharp;

namespace TAMMi.Core.Git;

/// <summary>
/// Reads repository status and history with LibGit2Sharp.
/// </summary>
public sealed class GitRepositoryReader : IGitRepositoryReader
{
    /// <inheritdoc />
    public RepositoryStatus ReadStatus(string path)
    {
        using Repository repository = Open(path);
        return ReadStatus(repository);
    }

    /// <inheritdoc />
    public IReadOnlyList<CommitInfo> ReadHistory(string path, HistoryOptions? options = null)
    {
        options ??= new HistoryOptions();
        using Repository repository = Open(path);

        object? startingPoints = options.AllRefs
            ? repository.Refs.Select(reference => reference.ResolveToDirectReference()?.Target)
                .OfType<Commit>()
                .Concat(repository.Tags.Select(tag => tag.PeeledTarget).OfType<Commit>())
                .Distinct()
                .ToArray()
            : repository.Head.Tip;

        if (startingPoints is null or Commit[] { Length: 0 })
        {
            return [];
        }

        IEnumerable<Commit> commits = repository.Commits.QueryBy(new CommitFilter
        {
            IncludeReachableFrom = startingPoints,
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time,
        });

        if (options.MaxCount is int maxCount)
        {
            commits = commits.Take(maxCount);
        }

        return commits.Select(ToCommitInfo).ToArray();
    }

    private static Repository Open(string path)
    {
        try
        {
            return new Repository(path);
        }
        catch (RepositoryNotFoundException ex)
        {
            throw new GitRepositoryNotFoundException(path, ex);
        }
    }

    private static RepositoryStatus ReadStatus(Repository repository)
    {
        Branch head = repository.Head;
        bool detached = repository.Info.IsHeadDetached;
        Branch? upstream = detached ? null : head.TrackedBranch;
        BranchTrackingDetails? tracking = upstream?.Tip is null ? null : head.TrackingDetails;

        return new RepositoryStatus
        {
            Path = Normalise(repository.Info.WorkingDirectory),
            Branch = detached ? null : head.FriendlyName,
            HeadSha = head.Tip?.Sha,
            Upstream = upstream?.FriendlyName,
            AheadBy = tracking?.AheadBy,
            BehindBy = tracking?.BehindBy,
            UnpushedCommitCount = CountUnpushedCommits(repository),
            Changes = ReadChanges(repository),
            Submodules = ReadSubmodules(repository),
        };
    }

    private static int CountUnpushedCommits(Repository repository)
    {
        Commit? tip = repository.Head.Tip;
        if (tip is null)
        {
            return 0;
        }

        Commit[] remoteTips = repository.Branches
            .Where(branch => branch.IsRemote)
            .Select(branch => branch.Tip)
            .OfType<Commit>()
            .Distinct()
            .ToArray();

        // Without remotes there's nowhere to push to, so nothing counts as unpushed.
        if (remoteTips.Length == 0)
        {
            return 0;
        }

        return repository.Commits.QueryBy(new CommitFilter
        {
            IncludeReachableFrom = tip,
            ExcludeReachableFrom = remoteTips,
        }).Count();
    }

    private static FileChange[] ReadChanges(Repository repository)
    {
        LibGit2Sharp.RepositoryStatus status = repository.RetrieveStatus(new StatusOptions
        {
            IncludeUntracked = true,
            RecurseUntrackedDirs = true,
            IncludeIgnored = false,
            ExcludeSubmodules = true,
            DetectRenamesInIndex = true,
            DetectRenamesInWorkDir = false,
        });

        return status
            .Where(entry => entry.State is not (FileStatus.Unaltered or FileStatus.Ignored))
            .Select(entry => new FileChange(
                entry.FilePath,
                StagedChange(entry.State),
                UnstagedChange(entry.State),
                entry.HeadToIndexRenameDetails?.OldFilePath))
            .OrderBy(change => change.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static FileChangeKind StagedChange(FileStatus state) => state switch
    {
        _ when state.HasFlag(FileStatus.Conflicted) => FileChangeKind.Conflicted,
        _ when state.HasFlag(FileStatus.NewInIndex) => FileChangeKind.Added,
        _ when state.HasFlag(FileStatus.ModifiedInIndex) => FileChangeKind.Modified,
        _ when state.HasFlag(FileStatus.DeletedFromIndex) => FileChangeKind.Deleted,
        _ when state.HasFlag(FileStatus.RenamedInIndex) => FileChangeKind.Renamed,
        _ when state.HasFlag(FileStatus.TypeChangeInIndex) => FileChangeKind.TypeChanged,
        _ => FileChangeKind.None,
    };

    private static FileChangeKind UnstagedChange(FileStatus state) => state switch
    {
        _ when state.HasFlag(FileStatus.Conflicted) => FileChangeKind.Conflicted,
        _ when state.HasFlag(FileStatus.NewInWorkdir) => FileChangeKind.Untracked,
        _ when state.HasFlag(FileStatus.ModifiedInWorkdir) => FileChangeKind.Modified,
        _ when state.HasFlag(FileStatus.DeletedFromWorkdir) => FileChangeKind.Deleted,
        _ when state.HasFlag(FileStatus.RenamedInWorkdir) => FileChangeKind.Renamed,
        _ when state.HasFlag(FileStatus.TypeChangeInWorkdir) => FileChangeKind.TypeChanged,
        _ => FileChangeKind.None,
    };

    private static SubmoduleStatus[] ReadSubmodules(Repository repository)
    {
        string root = repository.Info.WorkingDirectory;
        return repository.Submodules
            .Select(submodule => new SubmoduleStatus(
                submodule.Name,
                submodule.Path,
                CommitChange(submodule.RetrieveStatus()),
                ReadSubmoduleRepositoryStatus(Path.Combine(root, submodule.Path))))
            .OrderBy(submodule => submodule.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static SubmoduleCommitChange CommitChange(LibGit2Sharp.SubmoduleStatus status)
    {
        var change = SubmoduleCommitChange.None;
        if ((status & (LibGit2Sharp.SubmoduleStatus.IndexAdded
                | LibGit2Sharp.SubmoduleStatus.IndexDeleted
                | LibGit2Sharp.SubmoduleStatus.IndexModified)) != 0)
        {
            change |= SubmoduleCommitChange.Staged;
        }

        if ((status & (LibGit2Sharp.SubmoduleStatus.WorkDirAdded
                | LibGit2Sharp.SubmoduleStatus.WorkDirDeleted
                | LibGit2Sharp.SubmoduleStatus.WorkDirModified)) != 0)
        {
            change |= SubmoduleCommitChange.Unstaged;
        }

        return change;
    }

    private static RepositoryStatus? ReadSubmoduleRepositoryStatus(string path)
    {
        // An uninitialised submodule is an empty folder (or missing). IsValid doesn't search parent folders, so it
        // won't find the parent repository instead.
        if (!Directory.Exists(path) || !Repository.IsValid(path))
        {
            return null;
        }

        using var submoduleRepository = new Repository(path);
        return ReadStatus(submoduleRepository);
    }

    private static CommitInfo ToCommitInfo(Commit commit) => new(
        commit.Sha,
        commit.Parents.Select(parent => parent.Sha).ToArray(),
        ToSignature(commit.Author),
        ToSignature(commit.Committer),
        commit.MessageShort,
        commit.Message);

    private static CommitSignature ToSignature(Signature signature) =>
        new(signature.Name, signature.Email, signature.When);

    private static string Normalise(string workingDirectory) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(workingDirectory));
}
