using System.Runtime.InteropServices;
using LibGit2Sharp;

namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Reads repository status and history with LibGit2Sharp.
/// </summary>
public sealed class GitRepositoryReader : IGitRepositoryReader
{
    private static readonly Lazy<GitLibraryVersion> s_libraryVersion = new(ReadLibraryVersion);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int LibGit2VersionFunction(out int major, out int minor, out int revision);

    /// <inheritdoc />
    public GitLibraryVersion LibraryVersion => s_libraryVersion.Value;

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
        using IGitHistoryCursor cursor = OpenHistory(path, options);
        return cursor.ReadNext(options.MaxCount ?? int.MaxValue);
    }

    /// <inheritdoc />
    public IGitHistoryCursor OpenHistory(string path, HistoryOptions? options = null)
    {
        options ??= new HistoryOptions();
        Repository repository = Open(path);
        try
        {
            return new GitHistoryCursor(repository, FindStartingPoints(repository, options));
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public GitRefs ReadRefs(string path)
    {
        using Repository repository = Open(path);
        var references = new List<GitReference>();
        foreach (Branch branch in repository.Branches)
        {
            // Symbolic references such as origin/HEAD only repeat another branch.
            if (branch.Reference is DirectReference && branch.Tip is { } tip)
            {
                references.Add(new GitReference(
                    branch.FriendlyName,
                    branch.IsRemote ? GitReferenceKind.RemoteBranch : GitReferenceKind.LocalBranch,
                    tip.Sha));
            }
        }

        foreach (Tag tag in repository.Tags)
        {
            if (tag.PeeledTarget is Commit commit)
            {
                references.Add(new GitReference(tag.FriendlyName, GitReferenceKind.Tag, commit.Sha));
            }
        }

        return new GitRefs
        {
            HeadSha = repository.Head.Tip?.Sha,
            HeadBranch = repository.Info.IsHeadDetached || repository.Head.Tip is null
                ? null
                : repository.Head.FriendlyName,
            References = references
                .OrderBy(reference => reference.Kind)
                .ThenBy(reference => reference.Name, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<CommitFileChange> ReadCommitChanges(string path, string sha)
    {
        using Repository repository = Open(path);
        Commit commit = repository.Lookup<Commit>(sha)
            ?? throw new GitException($"There's no commit {sha} in '{path}'.");

        Tree? parentTree = commit.Parents.FirstOrDefault()?.Tree;
        TreeChanges changes = repository.Diff.Compare<TreeChanges>(
            parentTree,
            commit.Tree,
            new CompareOptions { Similarity = SimilarityOptions.Renames });

        return changes
            .Where(change => change.Status != ChangeKind.Unmodified)
            .Select(change => new CommitFileChange(
                change.Path,
                CommitChangeKind(change.Status),
                change.Status == ChangeKind.Renamed ? change.OldPath : null))
            .OrderBy(change => change.Path, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<GitRemote> ReadRemotes(string path)
    {
        using Repository repository = Open(path);
        return repository.Network.Remotes
            .Select(remote => new GitRemote(
                remote.Name,
                remote.Url,
                remote.PushUrl is { } pushUrl && pushUrl != remote.Url ? pushUrl : null))
            .OrderBy(remote => remote.Name, StringComparer.Ordinal)
            .ToArray();
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
        Branch? upstream = detached ? null : TrackedBranchOf(head);
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

    /// <summary>
    /// The branch's upstream, or <see langword="null"/> when it has none or git can't resolve it, e.g. when its remote
    /// is a URL rather than a configured remote, as <c>git push -u &lt;url&gt;</c> leaves it. Libgit2 throws for that,
    /// though git itself only stops showing an upstream.
    /// </summary>
    private static Branch? TrackedBranchOf(Branch? branch)
    {
        try
        {
            return branch?.TrackedBranch;
        }
        catch (LibGit2SharpException)
        {
            return null;
        }
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

    private static FileChangeKind CommitChangeKind(ChangeKind kind) => kind switch
    {
        ChangeKind.Added or ChangeKind.Copied => FileChangeKind.Added,
        ChangeKind.Deleted => FileChangeKind.Deleted,
        ChangeKind.Renamed => FileChangeKind.Renamed,
        ChangeKind.TypeChanged => FileChangeKind.TypeChanged,
        ChangeKind.Conflicted => FileChangeKind.Conflicted,
        _ => FileChangeKind.Modified,
    };

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

    private static Commit[] FindStartingPoints(Repository repository, HistoryOptions options)
    {
        if (options.AllRefs)
        {
            return repository.Refs.Select(reference => reference.ResolveToDirectReference()?.Target)
                .OfType<Commit>()
                .Concat(repository.Tags.Select(tag => tag.PeeledTarget).OfType<Commit>())
                .Distinct()
                .ToArray();
        }

        IReadOnlyList<string> names = options.StartingPoints.Count == 0 ? ["HEAD"] : options.StartingPoints;
        var commits = new List<Commit>();
        foreach (string name in names)
        {
            if (Resolve(repository, name) is { } commit)
            {
                commits.Add(commit);
            }

            if (options.IncludeUpstreams && TrackedBranchOf(FindLocalBranch(repository, name))?.Tip is { } upstream)
            {
                commits.Add(upstream);
            }
        }

        return commits.Distinct().ToArray();
    }

    private static Commit? Resolve(Repository repository, string name)
    {
        try
        {
            return repository.Lookup(name)?.Peel<Commit>();
        }
        catch (Exception ex) when (ex is LibGit2SharpException or ArgumentException)
        {
            // Not something git can resolve to a commit, e.g. an invalid name or a tag of a tree.
            return null;
        }
    }

    // The local branch a starting point names: HEAD's branch (unless it's detached), or a local branch by name.
    private static Branch? FindLocalBranch(Repository repository, string name)
    {
        if (name == "HEAD")
        {
            return repository.Info.IsHeadDetached ? null : repository.Head;
        }

        Branch? branch = repository.Branches[name];
        return branch is { IsRemote: false } ? branch : null;
    }

    internal static CommitInfo ToCommitInfo(Commit commit) => new(
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

    private static GitLibraryVersion ReadLibraryVersion()
    {
        LibGit2Sharp.Version version = GlobalSettings.Version;
        string libGit2SharpVersion = version.InformationalVersion.Split('+')[0];

        // The native library is named after the first 7 characters of the libgit2 commit.
        string commit = version.LibGit2CommitSha[..Math.Min(7, version.LibGit2CommitSha.Length)];
        return new GitLibraryVersion(libGit2SharpVersion, ReadLibGit2Version(commit), commit);
    }

    /// <summary>
    /// Asks the native library for its version. LibGit2Sharp doesn't expose it, only the commit it was built from.
    /// </summary>
    /// <param name="commitShortSha">Names the native library, e.g. <c>git2-5853918</c>.</param>
    private static string? ReadLibGit2Version(string commitShortSha)
    {
        // Load it the way LibGit2Sharp does, so this finds the same file.
        if (!NativeLibrary.TryLoad($"git2-{commitShortSha}", typeof(Repository).Assembly, null, out IntPtr library)
            || !NativeLibrary.TryGetExport(library, "git_libgit2_version", out IntPtr export))
        {
            return null;
        }

        var getVersion = Marshal.GetDelegateForFunctionPointer<LibGit2VersionFunction>(export);
        return getVersion(out int major, out int minor, out int revision) == 0
            ? $"{major}.{minor}.{revision}"
            : null;
    }
}
