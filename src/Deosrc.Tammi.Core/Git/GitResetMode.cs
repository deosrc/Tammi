namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// What <c>git reset</c> does besides moving the current branch to a commit.
/// </summary>
public enum GitResetMode
{
    /// <summary>
    /// Keeps the index and the working directory, so the changes since that commit stay staged.
    /// </summary>
    Soft,

    /// <summary>
    /// Resets the index but keeps the working directory, so the changes since that commit stay as unstaged changes.
    /// </summary>
    Mixed,

    /// <summary>
    /// Resets the index and the working directory, losing every change since that commit.
    /// </summary>
    Hard,
}
