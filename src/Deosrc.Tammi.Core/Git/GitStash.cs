using System.Globalization;

namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Stashes changes and brings them back with the <c>git</c> CLI.
/// </summary>
public sealed class GitStash(IGitCli git) : IGitStash
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GitStashEntry>> ListAsync(string path, CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await git.RunAsync(
            path,
            ["stash", "list", "--format=%gs%x1f%cI%x1f%P%x1f%H"],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var entries = new List<GitStashEntry>();
        foreach (string line in result.OutputLines)
        {
            string[] parts = line.Split('\x1f');
            DateTimeOffset createdAt = parts.Length > 1
                && DateTimeOffset.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed)
                    ? parsed
                    : default;
            // A stash's first parent is the commit it was made on.
            string baseSha = parts.Length > 2 ? parts[2].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "" : "";
            entries.Add(new GitStashEntry(entries.Count, parts[0], createdAt, baseSha, parts.Length > 3 ? parts[3] : ""));
        }

        return entries;
    }

    /// <inheritdoc />
    public async Task<bool> PushAsync(
        string path,
        IReadOnlyCollection<string>? files = null,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var arguments = new List<string> { "stash", "push", "--include-untracked" };
        if (!string.IsNullOrWhiteSpace(message))
        {
            arguments.Add("--message");
            arguments.Add(message.Trim());
        }

        if (files is { Count: > 0 })
        {
            arguments.Add("--");
            arguments.AddRange(files);
        }

        string before = await TopAsync(path, cancellationToken).ConfigureAwait(false);
        await git.RunAsync(path, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await TopAsync(path, cancellationToken).ConfigureAwait(false) != before;
    }

    /// <inheritdoc />
    public async Task ApplyAsync(string path, int index, CancellationToken cancellationToken = default) =>
        await git.RunAsync(path, ["stash", "apply", Name(index)], cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task PopAsync(string path, int index, CancellationToken cancellationToken = default) =>
        await git.RunAsync(path, ["stash", "pop", Name(index)], cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task DropAsync(string path, int index, CancellationToken cancellationToken = default) =>
        await git.RunAsync(path, ["stash", "drop", Name(index)], cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<string?> FindMergeBaseWithHeadAsync(string path, string sha, CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await git.RunAsync(
            path,
            ["merge-base", sha, "HEAD"],
            new GitCommandOptions { ThrowOnError = false },
            cancellationToken).ConfigureAwait(false);
        string found = result.StandardOutput.Trim();
        return result.Succeeded && found.Length > 0 ? found : null;
    }

    private static string Name(int index) => $"stash@{{{index}}}";

    /// <summary>
    /// The newest stash's sha, or "" if there are none. "git stash push" succeeds without making a stash when there's
    /// nothing to stash, so a changed sha is how to tell.
    /// </summary>
    private async Task<string> TopAsync(string path, CancellationToken cancellationToken)
    {
        GitCommandResult result = await git.RunAsync(
            path,
            ["rev-parse", "--verify", "--quiet", "refs/stash"],
            new GitCommandOptions { ThrowOnError = false },
            cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? result.StandardOutput.Trim() : "";
    }
}
