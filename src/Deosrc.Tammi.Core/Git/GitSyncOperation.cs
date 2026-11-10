namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// An operation that syncs a repository with its remote.
/// </summary>
public enum GitSyncOperation
{
    /// <summary>Downloads new commits and refs from the remotes, changing no branch.</summary>
    Fetch,

    /// <summary>Fetches the upstream branch and brings the checked-out branch up to date with it.</summary>
    Pull,

    /// <summary>Uploads the checked-out branch's commits to its remote.</summary>
    Push,
}
