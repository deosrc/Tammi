namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A stash: changes set aside with <c>git stash</c>.
/// </summary>
/// <param name="Index">The stash's position in the stash list, where 0 is the newest.</param>
/// <param name="Message">What the stash was given, e.g. <c>On main: Try a fix</c>.</param>
/// <param name="CreatedAt">When the stash was made.</param>
/// <param name="BaseSha">The commit that was HEAD when the stash was made.</param>
/// <param name="Sha">The stash's own commit, which holds the stashed changes: read its changed files and diffs like a commit's.</param>
public sealed record GitStashEntry(int Index, string Message, DateTimeOffset CreatedAt, string BaseSha, string Sha)
{
    /// <summary>
    /// The commit the git graph draws the stash on: <see cref="BaseSha"/>, or, when the graph doesn't show that commit
    /// (e.g. it's on a branch that isn't shown), the nearest commit it does show that the stash is built on.
    /// </summary>
    public string? AnchorSha { get; init; }

    /// <summary>
    /// The stash's name in git, e.g. <c>stash@{0}</c>.
    /// </summary>
    public string Name => $"stash@{{{Index}}}";
}
