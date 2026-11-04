using TAMMi.Core.Platform;

namespace TAMMi.Core.Git;

/// <summary>
/// Manages Workspace Worktrees with the <c>git</c> CLI.
/// </summary>
public sealed class GitWorktreeService(IGitCli git, IGitRepositoryReader reader) : IGitWorktreeService
{
    /// <summary>
    /// The remote used when none is given.
    /// </summary>
    public const string DefaultRemote = "origin";

    private const string GitDirPrefix = "gitdir:";
    private const string RemoteRefsPrefix = "refs/remotes/";

    private static readonly GitCommandOptions s_noThrow = new() { ThrowOnError = false };

    private static readonly StringComparison s_pathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <inheritdoc />
    public async Task FetchAsync(
        string repositoryPath,
        string remote = DefaultRemote,
        CancellationToken cancellationToken = default)
    {
        await git.RunAsync(repositoryPath, ["fetch", "--", remote], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> GetDefaultBranchAsync(
        string repositoryPath,
        string remote = DefaultRemote,
        CancellationToken cancellationToken = default)
    {
        string reference = await GetDefaultBranchReferenceAsync(repositoryPath, remote, cancellationToken)
            .ConfigureAwait(false);
        return reference[RemoteRefsPrefix.Length..];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListBranchesAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        const string localRefsPrefix = "refs/heads/";

        // Each line is "<full name>\0<symref target>", and the target is empty unless the reference is symbolic.
        GitCommandResult result = await git.RunAsync(
            repositoryPath,
            ["for-each-ref", "--format=%(refname)%00%(symref)", RemoteRefsPrefix, localRefsPrefix],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var remote = new List<string>();
        var local = new List<string>();
        foreach (string line in result.OutputLines)
        {
            string[] parts = line.Split('\0', 2);
            if (parts.Length == 2 && parts[1].Length > 0)
            {
                continue;
            }

            if (parts[0].StartsWith(RemoteRefsPrefix, StringComparison.Ordinal))
            {
                remote.Add(parts[0][RemoteRefsPrefix.Length..]);
            }
            else if (parts[0].StartsWith(localRefsPrefix, StringComparison.Ordinal))
            {
                local.Add(parts[0][localRefsPrefix.Length..]);
            }
        }

        remote.Sort(StringComparer.Ordinal);
        local.Sort(StringComparer.Ordinal);
        return [.. remote, .. local];
    }

    /// <inheritdoc />
    public async Task<bool> CommitExistsAsync(
        string repositoryPath,
        string commitish,
        CancellationToken cancellationToken = default)
    {
        // A leading dash would be read as an option.
        if (string.IsNullOrWhiteSpace(commitish) || commitish.StartsWith('-'))
        {
            return false;
        }

        GitCommandResult result = await git.RunAsync(
            repositoryPath,
            ["rev-parse", "--verify", "--quiet", $"{commitish}^{{commit}}"],
            s_noThrow,
            cancellationToken).ConfigureAwait(false);
        return result.Succeeded;
    }

    /// <inheritdoc />
    public async Task<bool> IsValidBranchNameAsync(string branchName, CancellationToken cancellationToken = default)
    {
        // A leading dash would be read as an option.
        if (branchName.Length == 0 || branchName.StartsWith('-'))
        {
            return false;
        }

        GitCommandResult result = await git.RunAsync(
            Directory.GetCurrentDirectory(),
            ["check-ref-format", "--branch", branchName],
            s_noThrow,
            cancellationToken).ConfigureAwait(false);

        // "--branch" also accepts "@{-1}" style references to previous branches, which can't be created.
        return result.Succeeded && !branchName.Contains("@{", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public async Task<string> CreateWorktreeAsync(
        CreateWorktreeRequest request,
        CancellationToken cancellationToken = default)
    {
        string repository = Path.GetFullPath(request.RepositoryPath);
        string worktree = Path.GetFullPath(request.WorktreePath);

        if (request.Fetch)
        {
            await FetchAsync(repository, request.Remote, cancellationToken).ConfigureAwait(false);
        }

        string baseBranch = request.BaseBranch
            ?? await GetDefaultBranchReferenceAsync(repository, request.Remote, cancellationToken)
                .ConfigureAwait(false);

        // --no-track: the branch is new, so it's published under its own name rather than pushed to the base.
        await git.RunAsync(
            repository,
            ["worktree", "add", "--no-track", "-b", request.BranchName, worktree, baseBranch],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (request.InitSubmodules)
        {
            await InitSubmodulesAsync(worktree, cancellationToken).ConfigureAwait(false);
        }

        return worktree;
    }

    /// <inheritdoc />
    public async Task InitSubmodulesAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        await git.RunAsync(
            worktreePath,
            ["submodule", "update", "--init", "--recursive"],
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveWorktreeAsync(
        string repositoryPath,
        string worktreePath,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        string repository = Path.GetFullPath(repositoryPath);
        WorktreeCheck check = CheckWorktree(worktreePath);

        if (!force)
        {
            EnsureNoLostWork(check);
        }

        switch (check.Health)
        {
            case WorktreeHealth.Missing:
                if (Directory.Exists(repository))
                {
                    // Unregisters the worktree. Git allows this for a folder that no longer exists.
                    await RemoveRegisteredWorktreeAsync(repository, check.Path, cancellationToken)
                        .ConfigureAwait(false);
                }

                break;

            case WorktreeHealth.Moved:
                // Git only knows the old location, so it can't find the worktree to remove until it's repaired.
                await git.RunAsync(check.Path, ["worktree", "repair"], cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                await RemoveRegisteredWorktreeAsync(repository, check.Path, cancellationToken).ConfigureAwait(false);
                break;

            case WorktreeHealth.Healthy:
                await RemoveRegisteredWorktreeAsync(repository, check.Path, cancellationToken).ConfigureAwait(false);
                break;

            case WorktreeHealth.RepositoryMissing or WorktreeHealth.Unregistered:
                // Git has no record of the worktree any more, so only its folder is left to delete.
                FileSystemDeletion.DeleteFolder(check.Path);
                break;

            default:
                throw new GitException($"'{check.Path}' isn't a git worktree, so it wasn't removed.");
        }
    }

    /// <inheritdoc />
    public async Task<BranchState> GetBranchStateAsync(
        string repositoryPath,
        string branchName,
        string? mergeTarget = null,
        CancellationToken cancellationToken = default)
    {
        string branch = $"refs/heads/{branchName}";
        GitCommandResult exists = await git.RunAsync(
            repositoryPath,
            ["show-ref", "--verify", "--quiet", branch],
            s_noThrow,
            cancellationToken).ConfigureAwait(false);
        if (!exists.Succeeded)
        {
            return BranchState.Missing;
        }

        bool isMerged = await IsMergedAsync(repositoryPath, branch, mergeTarget, cancellationToken)
            .ConfigureAwait(false);

        // Commits that aren't on any remote-tracking branch, matching RepositoryStatus.UnpushedCommitCount.
        GitCommandResult unpushed = await git.RunAsync(
            repositoryPath,
            ["rev-list", "--count", branch, "--not", "--remotes"],
            cancellationToken: cancellationToken).ConfigureAwait(false);
        bool isPushed = unpushed.StandardOutput.Trim() == "0";

        return new BranchState(true, isMerged, isPushed);
    }

    /// <inheritdoc />
    public async Task DeleteBranchAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        await git.RunAsync(repositoryPath, ["branch", "-D", "--", branchName], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public WorktreeCheck CheckWorktree(string worktreePath)
    {
        string worktree = Path.TrimEndingDirectorySeparator(Path.GetFullPath(worktreePath));
        if (!Directory.Exists(worktree))
        {
            return new WorktreeCheck(worktree, WorktreeHealth.Missing, null, null);
        }

        // A worktree has a .git file pointing at <repo>/.git/worktrees/<name>. A clone has a .git folder, and a
        // submodule's .git file points at <repo>/.git/modules/<name>.
        string dotGit = Path.Combine(worktree, ".git");
        string? gitDir = File.Exists(dotGit) ? ReadGitDirFile(dotGit) : null;
        string? worktreesFolder = gitDir is null ? null : Path.GetDirectoryName(gitDir);
        if (worktreesFolder is null || Path.GetFileName(worktreesFolder) != "worktrees")
        {
            return new WorktreeCheck(worktree, WorktreeHealth.NotAWorktree, null, null);
        }

        string commonDir = Path.GetDirectoryName(worktreesFolder)!;
        string repository = Path.GetFileName(commonDir) == ".git" ? Path.GetDirectoryName(commonDir)! : commonDir;

        if (!Directory.Exists(gitDir))
        {
            WorktreeHealth health = Directory.Exists(commonDir)
                ? WorktreeHealth.Unregistered
                : WorktreeHealth.RepositoryMissing;
            return new WorktreeCheck(worktree, health, gitDir, repository);
        }

        // The repository records where the worktree's .git file is, so it can find the worktree.
        string backLinkFile = Path.Combine(gitDir!, "gitdir");
        string? backLink = File.Exists(backLinkFile) ? ResolvePath(gitDir!, File.ReadAllText(backLinkFile)) : null;
        WorktreeHealth linkHealth = string.Equals(backLink, dotGit, s_pathComparison)
            ? WorktreeHealth.Healthy
            : WorktreeHealth.Moved;
        return new WorktreeCheck(worktree, linkHealth, gitDir, repository);
    }

    /// <inheritdoc />
    public async Task<WorktreeCheck> RepairWorktreeAsync(
        string worktreePath,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        WorktreeCheck before = CheckWorktree(worktreePath);
        if (before.Health is WorktreeHealth.Missing or WorktreeHealth.NotAWorktree)
        {
            throw new GitException($"'{before.Path}' can't be repaired: {Describe(before.Health)}");
        }

        if (repositoryPath is not null)
        {
            // Run from the repository's new location, given the worktree, to point the worktree at it.
            string repository = Path.GetFullPath(repositoryPath);
            if (!Directory.Exists(repository))
            {
                throw new GitRepositoryNotFoundException(repository);
            }

            await git.RunAsync(repository, ["worktree", "repair", before.Path], cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        else if (before.Health != WorktreeHealth.Healthy)
        {
            // Run from the worktree, to point the repository at the worktree's new location.
            await git.RunAsync(before.Path, ["worktree", "repair"], cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        WorktreeCheck after = CheckWorktree(before.Path);
        if (after.IsBroken)
        {
            throw new GitException($"'{after.Path}' couldn't be repaired: {Describe(after.Health)}");
        }

        // git worktree repair doesn't fix submodules, whose links to their git directories are relative. Git can't
        // run inside a submodule with a broken link, so run it in the worktree's git directory, which now works.
        await RepairSubmoduleLinksAsync(after.Path, after.GitDirectory!, after.GitDirectory!, cancellationToken)
            .ConfigureAwait(false);
        return after;
    }

    private async Task<string> GetDefaultBranchReferenceAsync(
        string repositoryPath,
        string remote,
        CancellationToken cancellationToken)
    {
        // Set by clone (and by "git remote set-head"), e.g. refs/remotes/origin/main.
        GitCommandResult local = await git.RunAsync(
            repositoryPath,
            ["symbolic-ref", "--quiet", $"{RemoteRefsPrefix}{remote}/HEAD"],
            s_noThrow,
            cancellationToken).ConfigureAwait(false);
        if (local.Succeeded && local.OutputLines is [string localReference, ..])
        {
            return localReference;
        }

        // Not set, e.g. the remote was added by hand. Ask the remote, without changing the repository.
        GitCommandResult remoteHead = await git.RunAsync(
            repositoryPath,
            ["ls-remote", "--symref", "--", remote, "HEAD"],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // e.g. "ref: refs/heads/main\tHEAD"
        const string symrefPrefix = "ref: refs/heads/";
        string? head = remoteHead.OutputLines
            .Where(line => line.StartsWith(symrefPrefix, StringComparison.Ordinal) && line.EndsWith("\tHEAD"))
            .Select(line => line[symrefPrefix.Length..^"\tHEAD".Length])
            .FirstOrDefault();
        if (head is not null)
        {
            string reference = $"{RemoteRefsPrefix}{remote}/{head}";
            GitCommandResult exists = await git.RunAsync(
                repositoryPath,
                ["show-ref", "--verify", "--quiet", reference],
                s_noThrow,
                cancellationToken).ConfigureAwait(false);
            if (exists.Succeeded)
            {
                return reference;
            }
        }

        throw new GitException($"Couldn't find the default branch of remote '{remote}' in '{repositoryPath}'.");
    }

    private async Task<bool> IsMergedAsync(
        string repositoryPath,
        string branch,
        string? mergeTarget,
        CancellationToken cancellationToken)
    {
        if (mergeTarget is null)
        {
            try
            {
                mergeTarget = await GetDefaultBranchReferenceAsync(repositoryPath, DefaultRemote, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (GitException)
            {
                // Nothing to be merged into, e.g. the repository has no origin remote.
                return false;
            }
        }

        // Exit code 0: an ancestor, 1: not an ancestor, anything else: an error such as an unknown target.
        GitCommandResult result = await git.RunAsync(
            repositoryPath,
            ["merge-base", "--is-ancestor", branch, mergeTarget],
            s_noThrow,
            cancellationToken).ConfigureAwait(false);
        return result.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => throw new GitCommandException(result),
        };
    }

    private void EnsureNoLostWork(WorktreeCheck check)
    {
        switch (check.Health)
        {
            case WorktreeHealth.Missing:
                // The folder is gone, so there's nothing left to lose.
                return;

            case WorktreeHealth.Healthy or WorktreeHealth.Moved:
                RepositoryStatus status = reader.ReadStatus(check.Path);
                if (status.IsChanged)
                {
                    throw new WorktreeChangedException(status);
                }

                return;

            case WorktreeHealth.NotAWorktree:
                // Removing it fails anyway.
                return;

            default:
                throw new GitException(
                    $"'{check.Path}' can't be checked for changes: {Describe(check.Health)} Remove it with force to delete it anyway.");
        }
    }

    private async Task RemoveRegisteredWorktreeAsync(
        string repository,
        string worktree,
        CancellationToken cancellationToken)
    {
        // --force: TAMMi checks for lost work itself, including inside submodules, and git refuses to remove any
        // worktree with checked-out submodules without it. A single --force still keeps locked worktrees.
        await git.RunAsync(
            repository,
            ["worktree", "remove", "--force", worktree],
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task RepairSubmoduleLinksAsync(
        string workTree,
        string gitDir,
        string gitWorkingDirectory,
        CancellationToken cancellationToken)
    {
        string gitModules = Path.Combine(workTree, ".gitmodules");
        if (!File.Exists(gitModules))
        {
            return;
        }

        IReadOnlyList<(string Name, string Path)> submodules =
            await ReadSubmodulePathsAsync(gitModules, gitWorkingDirectory, cancellationToken).ConfigureAwait(false);
        foreach ((string name, string path) in submodules)
        {
            // Git keeps a submodule's git directory at <parent git dir>/modules/<name>.
            string submoduleWorkTree = Path.GetFullPath(Path.Combine(workTree, path));
            string submoduleGitDir = Path.GetFullPath(Path.Combine(gitDir, "modules", name));
            string dotGit = Path.Combine(submoduleWorkTree, ".git");
            if (!File.Exists(dotGit) || !Directory.Exists(submoduleGitDir))
            {
                // Not checked out.
                continue;
            }

            if (!string.Equals(ReadGitDirFile(dotGit), submoduleGitDir, s_pathComparison))
            {
                await File.WriteAllTextAsync(
                    dotGit,
                    $"{GitDirPrefix} {RelativePath(submoduleWorkTree, submoduleGitDir)}\n",
                    cancellationToken).ConfigureAwait(false);
            }

            string config = Path.Combine(submoduleGitDir, "config");
            GitCommandResult coreWorktree = await git.RunAsync(
                gitWorkingDirectory,
                ["config", "--file", config, "--get", "core.worktree"],
                s_noThrow,
                cancellationToken).ConfigureAwait(false);
            string? recorded = coreWorktree.Succeeded
                ? ResolvePath(submoduleGitDir, coreWorktree.StandardOutput)
                : null;
            if (!string.Equals(recorded, submoduleWorkTree, s_pathComparison))
            {
                await git.RunAsync(
                    gitWorkingDirectory,
                    ["config", "--file", config, "core.worktree", RelativePath(submoduleGitDir, submoduleWorkTree)],
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            await RepairSubmoduleLinksAsync(submoduleWorkTree, submoduleGitDir, gitWorkingDirectory, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<(string Name, string Path)>> ReadSubmodulePathsAsync(
        string gitModules,
        string gitWorkingDirectory,
        CancellationToken cancellationToken)
    {
        GitCommandResult result = await git.RunAsync(
            gitWorkingDirectory,
            ["config", "--file", gitModules, "--null", "--get-regexp", @"^submodule\..*\.path$"],
            s_noThrow,
            cancellationToken).ConfigureAwait(false);

        // Exit code 1 means no submodules. Each entry is "submodule.<name>.path\n<path>", and names can contain dots.
        const string keyPrefix = "submodule.";
        const string keySuffix = ".path";
        return result.OutputFields
            .Select(field => field.Split('\n', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => (Name: parts[0][keyPrefix.Length..^keySuffix.Length], Path: parts[1]))
            .ToArray();
    }

    private static string? ReadGitDirFile(string dotGitFile)
    {
        string content = File.ReadAllText(dotGitFile);
        string firstLine = content.Split('\n', 2)[0].Trim();
        if (!firstLine.StartsWith(GitDirPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return ResolvePath(Path.GetDirectoryName(dotGitFile)!, firstLine[GitDirPrefix.Length..]);
    }

    /// <summary>
    /// Resolves a path git wrote to a file, which may be relative to <paramref name="baseDirectory"/>.
    /// </summary>
    private static string ResolvePath(string baseDirectory, string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(baseDirectory, path.Trim())));

    /// <summary>
    /// A relative path in the form git writes, with <c>/</c> separators on every platform.
    /// </summary>
    private static string RelativePath(string from, string to) =>
        Path.GetRelativePath(from, to).Replace('\\', '/');

    private static string Describe(WorktreeHealth health) => health switch
    {
        WorktreeHealth.Missing => "the folder doesn't exist.",
        WorktreeHealth.NotAWorktree => "it isn't a git worktree.",
        WorktreeHealth.RepositoryMissing => "its local repository was moved or deleted.",
        WorktreeHealth.Unregistered => "its local repository has no record of it.",
        WorktreeHealth.Moved => "it was moved, and its local repository has the old location.",
        _ => "it's healthy.",
    };
}
