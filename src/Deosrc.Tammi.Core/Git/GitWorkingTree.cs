namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Stages, unstages and commits with the <c>git</c> CLI.
/// </summary>
public sealed class GitWorkingTree(IGitCli git) : IGitWorkingTree
{
    private const string MessageFileName = "COMMIT_EDITMSG";

    /// <inheritdoc />
    public async Task StageAsync(string path, IReadOnlyCollection<string> files, CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
        {
            return;
        }

        await git.RunAsync(path, ["add", "-A", "--", .. files], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UnstageAsync(string path, IReadOnlyCollection<string> files, CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
        {
            return;
        }

        // Before the first commit there's no HEAD to restore from, so the files come out of the index instead.
        GitCommandResult head = await git.RunAsync(
            path,
            ["rev-parse", "--verify", "--quiet", "HEAD"],
            new GitCommandOptions { ThrowOnError = false },
            cancellationToken).ConfigureAwait(false);
        string[] arguments = head.ExitCode == 0
            ? ["restore", "--staged", "--", .. files]
            : ["rm", "--cached", "-r", "--quiet", "--ignore-unmatch", "--", .. files];
        await git.RunAsync(path, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DiscardAsync(
        string path,
        IReadOnlyCollection<string> tracked,
        IReadOnlyCollection<string> untracked,
        CancellationToken cancellationToken = default)
    {
        if (tracked.Count > 0)
        {
            await git.RunAsync(path, ["restore", "--", .. tracked], cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        if (untracked.Count > 0)
        {
            await git.RunAsync(path, ["clean", "-f", "-d", "--quiet", "--", .. untracked], cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task DiscardAllAsync(string path, CancellationToken cancellationToken = default)
    {
        await git.RunAsync(path, ["restore", "--", "."], cancellationToken: cancellationToken).ConfigureAwait(false);
        await git.RunAsync(path, ["clean", "-f", "-d", "--quiet"], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ResetAsync(string path, string target, GitResetMode mode, CancellationToken cancellationToken = default)
    {
        string option = mode switch
        {
            GitResetMode.Soft => "--soft",
            GitResetMode.Hard => "--hard",
            _ => "--mixed",
        };
        await git.RunAsync(path, ["reset", option, "--quiet", target, "--"], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CommitAsync(string path, string message, CancellationToken cancellationToken = default)
    {
        await git.RunAsync(
            path,
            ["commit", "--cleanup=strip", "-F", "-"],
            new GitCommandOptions { StandardInput = message },
            cancellationToken).ConfigureAwait(false);
        await WriteMessageAsync(path, "", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> ReadMessageAsync(string path, CancellationToken cancellationToken = default)
    {
        string file = await MessageFileAsync(path, cancellationToken).ConfigureAwait(false);
        return File.Exists(file)
            ? (await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false)).TrimEnd('\r', '\n')
            : "";
    }

    /// <inheritdoc />
    public async Task WriteMessageAsync(string path, string message, CancellationToken cancellationToken = default)
    {
        string file = await MessageFileAsync(path, cancellationToken).ConfigureAwait(false);
        if (message.Length == 0)
        {
            File.Delete(file);
            return;
        }

        await File.WriteAllTextAsync(file, message, cancellationToken).ConfigureAwait(false);
    }

    // The git directory differs for a worktree or submodule, where the repository's .git is a file pointing elsewhere.
    private async Task<string> MessageFileAsync(string path, CancellationToken cancellationToken)
    {
        GitCommandResult result = await git.RunAsync(
            path,
            ["rev-parse", "--absolute-git-dir"],
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return Path.Combine(result.StandardOutput.Trim(), MessageFileName);
    }
}
