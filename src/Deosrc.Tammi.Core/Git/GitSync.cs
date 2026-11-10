namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Fetches, pulls and pushes with the <c>git</c> CLI.
/// </summary>
public sealed class GitSync(IGitCli git) : IGitSync
{
    /// <inheritdoc />
    public Task FetchAsync(string path, IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
        RunAsync(GitSyncOperation.Fetch, path, ["fetch", "--all", "--prune", "--recurse-submodules=yes", "--progress"], progress, cancellationToken);

    /// <inheritdoc />
    public async Task PullAsync(string path, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await RequireBranchAsync(GitSyncOperation.Pull, path, cancellationToken).ConfigureAwait(false);

        List<string> arguments = ["pull", "--progress", "--no-edit"];

        // Git refuses to pull diverged branches unless it's told how to combine them. Merging is git's classic
        // behaviour, so it's what an unconfigured pull does here, and the conflicts show in the working changes.
        if (!await HasConfigAsync(path, "pull.rebase", cancellationToken).ConfigureAwait(false)
            && !await HasConfigAsync(path, "pull.ff", cancellationToken).ConfigureAwait(false))
        {
            arguments.Add("--no-rebase");
        }

        await RunAsync(GitSyncOperation.Pull, path, arguments, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PushAsync(string path, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await RequireBranchAsync(GitSyncOperation.Push, path, cancellationToken).ConfigureAwait(false);

        GitCommandResult upstream = await git.RunAsync(
            path,
            ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}"],
            new GitCommandOptions { ThrowOnError = false },
            cancellationToken).ConfigureAwait(false);

        List<string> arguments = ["push", "--progress"];
        if (!upstream.Succeeded)
        {
            string remote = await ChooseRemoteAsync(path, cancellationToken).ConfigureAwait(false);
            arguments.AddRange(["--set-upstream", remote, "HEAD"]);
        }

        await RunAsync(GitSyncOperation.Push, path, arguments, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(
        GitSyncOperation operation,
        string path,
        IReadOnlyList<string> arguments,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        GitCommandResult result = await git.RunAsync(
            path,
            arguments,
            new GitCommandOptions
            {
                ThrowOnError = false,
                OnErrorLine = progress is null ? null : line => progress.Report(line.Trim()),
            },
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw GitSyncErrors.Classify(operation, result);
        }
    }

    // Pulling and pushing need a branch. A submodule is usually on a detached HEAD, so say so rather than showing
    // git's advice about setting up tracking.
    private async Task RequireBranchAsync(GitSyncOperation operation, string path, CancellationToken cancellationToken)
    {
        GitCommandResult branch = await git.RunAsync(
            path,
            ["symbolic-ref", "--quiet", "HEAD"],
            new GitCommandOptions { ThrowOnError = false },
            cancellationToken).ConfigureAwait(false);
        if (!branch.Succeeded)
        {
            throw new GitSyncException(
                operation,
                GitSyncFailure.DetachedHead,
                $"No branch is checked out, so there's nothing to {operation.ToString().ToLowerInvariant()}. Check out a branch first.",
                "");
        }
    }

    private async Task<string> ChooseRemoteAsync(string path, CancellationToken cancellationToken)
    {
        GitCommandResult result = await git.RunAsync(path, ["remote"], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> remotes = result.OutputLines;
        if (remotes.Count == 0)
        {
            throw new GitSyncException(
                GitSyncOperation.Push,
                GitSyncFailure.NoRemote,
                "This repository has no remote to push to.",
                "");
        }

        return remotes.Contains("origin") ? "origin" : remotes[0];
    }

    private async Task<bool> HasConfigAsync(string path, string key, CancellationToken cancellationToken)
    {
        GitCommandResult result = await git.RunAsync(
            path,
            ["config", "--get", key],
            new GitCommandOptions { ThrowOnError = false },
            cancellationToken).ConfigureAwait(false);
        return result.Succeeded;
    }
}
