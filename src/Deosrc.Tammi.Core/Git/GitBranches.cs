namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Creates, checks out, renames and deletes branches with the <c>git</c> CLI.
/// </summary>
public sealed class GitBranches(IGitCli git) : IGitBranches
{
    /// <inheritdoc />
    public async Task CreateAsync(string path, string name, string startPoint, bool checkout, CancellationToken cancellationToken = default)
    {
        string[] arguments = checkout
            ? ["switch", "--create", name, "--no-track", startPoint]
            : ["branch", "--no-track", "--", name, startPoint];
        await git.RunAsync(path, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CheckoutAsync(string path, string name, CancellationToken cancellationToken = default)
    {
        // "git switch" creates a tracking branch when the name matches one remote's branch, but not for "origin/x".
        string? remoteBranch = await FindRemoteBranchAsync(path, name, cancellationToken).ConfigureAwait(false);
        string[] arguments = remoteBranch is null
            ? ["switch", name]
            : ["switch", "--create", remoteBranch[(remoteBranch.IndexOf('/') + 1)..], "--track", remoteBranch];
        await git.RunAsync(path, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task MergeAsync(string path, string name, CancellationToken cancellationToken = default) =>
        await git.RunAsync(path, ["merge", "--no-edit", "--", name], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RebaseAsync(string path, string name, CancellationToken cancellationToken = default) =>
        await git.RunAsync(path, ["rebase", name, "--"], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RenameAsync(string path, string name, string newName, CancellationToken cancellationToken = default) =>
        await git.RunAsync(path, ["branch", "--move", "--", name, newName], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<GitUpstream?> GetUpstreamAsync(string path, string name, CancellationToken cancellationToken = default)
    {
        GitCommandResult remote = await git.RunAsync(
            path, ["config", "--get", $"branch.{name}.remote"], new GitCommandOptions { ThrowOnError = false }, cancellationToken)
            .ConfigureAwait(false);
        GitCommandResult merge = await git.RunAsync(
            path, ["config", "--get", $"branch.{name}.merge"], new GitCommandOptions { ThrowOnError = false }, cancellationToken)
            .ConfigureAwait(false);
        const string prefix = "refs/heads/";
        string remoteName = remote.StandardOutput.Trim();
        string mergeRef = merge.StandardOutput.Trim();
        if (!remote.Succeeded || !merge.Succeeded || remoteName.Length == 0 || remoteName == "." || !mergeRef.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return new GitUpstream(remoteName, mergeRef[prefix.Length..]);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string path, string name, bool deleteLocal, bool deleteRemote, CancellationToken cancellationToken = default)
    {
        GitUpstream? upstream = deleteRemote
            ? await GetUpstreamAsync(path, name, cancellationToken).ConfigureAwait(false)
            : null;
        if (deleteRemote && upstream is null)
        {
            throw new GitException($"The branch {name} has no remote branch to delete.");
        }

        if (deleteLocal)
        {
            await git.RunAsync(path, ["branch", "--delete", "--force", "--", name], cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        if (upstream is not null)
        {
            await PushDeleteAsync(path, upstream.Remote, upstream.Branch, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task DeleteRemoteAsync(string path, string remoteBranch, CancellationToken cancellationToken = default)
    {
        int slash = remoteBranch.IndexOf('/');
        if (slash <= 0 || slash == remoteBranch.Length - 1)
        {
            throw new GitException($"{remoteBranch} isn't a remote branch.");
        }

        await PushDeleteAsync(path, remoteBranch[..slash], remoteBranch[(slash + 1)..], cancellationToken).ConfigureAwait(false);
    }

    private async Task PushDeleteAsync(string path, string remote, string branch, CancellationToken cancellationToken) =>
        await git.RunAsync(path, ["push", remote, "--delete", $"refs/heads/{branch}"], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// The name, if it's a remote-tracking branch (refs/remotes/&lt;name&gt;) and not also a local branch.
    /// </summary>
    private async Task<string?> FindRemoteBranchAsync(string path, string name, CancellationToken cancellationToken)
    {
        GitCommandResult local = await git.RunAsync(
            path, ["show-ref", "--verify", "--quiet", $"refs/heads/{name}"], new GitCommandOptions { ThrowOnError = false }, cancellationToken)
            .ConfigureAwait(false);
        if (local.Succeeded)
        {
            return null;
        }

        GitCommandResult remote = await git.RunAsync(
            path, ["show-ref", "--verify", "--quiet", $"refs/remotes/{name}"], new GitCommandOptions { ThrowOnError = false }, cancellationToken)
            .ConfigureAwait(false);
        return remote.Succeeded ? name : null;
    }
}
