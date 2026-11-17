namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Reads a file's diff with <see cref="IGitRepositoryReader"/>, or with the git CLI when whitespace is ignored.
/// </summary>
public sealed class GitDiffs(IGitRepositoryReader reader, IGitCli git) : IGitDiffs
{
    /// <inheritdoc />
    public async Task<FileDiff> ReadAsync(
        string path,
        GitDiffSource source,
        string file,
        GitDiffOptions options,
        string? oldPath = null,
        string? sha = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        FileDiff real = reader.ReadDiff(path, source, file, oldPath, sha, options.ContextLines);
        if (!options.NeedsCli || real.IsBinary || real.Hunks.Count == 0 || IsOnlyAdditions(real))
        {
            // Nothing to ignore: a new file is all additions, whatever the whitespace.
            return real;
        }

        string[] paths = oldPath is null ? [file] : [file, oldPath];
        string[] arguments = source switch
        {
            GitDiffSource.Commit =>
                ["show", "--format=", "--first-parent", "-m", "--no-color", "--no-ext-diff", "-M", .. options.ToArguments(), sha ?? "", "--", .. paths],
            GitDiffSource.Staged =>
                ["diff", "--cached", "--no-color", "--no-ext-diff", "-M", .. options.ToArguments(), "--", .. paths],
            _ => ["diff", "--no-color", "--no-ext-diff", .. options.ToArguments(), "--", .. paths],
        };

        GitCommandResult result = await git.RunAsync(path, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
        (IReadOnlyList<DiffHunk> hunks, bool truncated) = FileDiffParser.Parse(result.StandardOutput);
        return new FileDiff(real.Path, real.OldPath, hunks, IsTruncated: truncated);
    }

    private static bool IsOnlyAdditions(FileDiff diff) =>
        diff.Hunks.All(hunk => hunk.Lines.All(line => line.Kind is DiffLineKind.Added or DiffLineKind.Note));
}
