namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Why a <see cref="GitSyncOperation"/> failed, as far as git's message says.
/// </summary>
public enum GitSyncFailure
{
    /// <summary>Something else. The message has git's own words.</summary>
    Other,

    /// <summary>The remote refused the credentials, or none were available.</summary>
    Authentication,

    /// <summary>The remote couldn't be reached, or doesn't exist.</summary>
    Network,

    /// <summary>The merge stopped with conflicts to resolve.</summary>
    Conflict,

    /// <summary>The branch and its upstream have each changed, and git was told not to pick how to combine them.</summary>
    Diverged,

    /// <summary>Uncommitted changes would be overwritten by the pull.</summary>
    LocalChanges,

    /// <summary>The remote has commits the branch doesn't, so it refused the push. Pull first.</summary>
    PushRejected,

    /// <summary>No branch is checked out (HEAD is detached), so there's nothing to pull into or push.</summary>
    DetachedHead,

    /// <summary>The repository has no remote to sync with.</summary>
    NoRemote,

    /// <summary>The branch has no upstream to pull from.</summary>
    NoUpstream,
}
