namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Creates, pushes and deletes tags with the <c>git</c> CLI.
/// </summary>
public sealed class GitTags(IGitCli git) : IGitTags
{
    /// <inheritdoc />
    public async Task CreateAsync(string path, string name, string sha, CancellationToken cancellationToken = default) =>
        await git.RunAsync(path, ["tag", "--", name, sha], cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<string?> GetRemoteAsync(string path, CancellationToken cancellationToken = default) =>
        ChooseRemoteAsync(path, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ExistsOnRemoteAsync(string path, string name, CancellationToken cancellationToken = default)
    {
        string? remote = await ChooseRemoteAsync(path, cancellationToken).ConfigureAwait(false);
        if (remote is null)
        {
            return false;
        }

        GitCommandResult result = await git.RunAsync(
            path,
            ["ls-remote", "--tags", "--exit-code", remote, $"refs/tags/{name}"],
            new GitCommandOptions { ThrowOnError = false },
            cancellationToken).ConfigureAwait(false);
        return result.Succeeded;
    }

    /// <inheritdoc />
    public async Task PushAsync(string path, string name, CancellationToken cancellationToken = default)
    {
        string remote = await ChooseRemoteAsync(path, cancellationToken).ConfigureAwait(false)
            ?? throw new GitException("This repository has no remote to push to.");
        await git.RunAsync(path, ["push", remote, $"refs/tags/{name}:refs/tags/{name}"], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string path, string name, CancellationToken cancellationToken = default)
    {
        string? remote = await ChooseRemoteAsync(path, cancellationToken).ConfigureAwait(false);
        bool onRemote = remote is not null
            && await ExistsOnRemoteAsync(path, name, cancellationToken).ConfigureAwait(false);

        await git.RunAsync(path, ["tag", "--delete", "--", name], cancellationToken: cancellationToken).ConfigureAwait(false);
        if (onRemote)
        {
            await git.RunAsync(path, ["push", remote!, "--delete", $"refs/tags/{name}"], cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<string?> ChooseRemoteAsync(string path, CancellationToken cancellationToken)
    {
        GitCommandResult result = await git.RunAsync(path, ["remote"], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> remotes = result.OutputLines;
        return remotes.Count == 0 ? null : remotes.Contains("origin") ? "origin" : remotes[0];
    }
}
