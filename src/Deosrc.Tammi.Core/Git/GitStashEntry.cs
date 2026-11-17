namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A stash: changes set aside with <c>git stash</c>.
/// </summary>
/// <param name="Index">The stash's position in the stash list, where 0 is the newest.</param>
/// <param name="Message">What the stash was given, e.g. <c>On main: Try a fix</c>.</param>
/// <param name="CreatedAt">When the stash was made.</param>
public sealed record GitStashEntry(int Index, string Message, DateTimeOffset CreatedAt)
{
    /// <summary>
    /// The stash's name in git, e.g. <c>stash@{0}</c>.
    /// </summary>
    public string Name => $"stash@{{{Index}}}";
}
