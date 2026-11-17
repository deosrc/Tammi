using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A request to reset the current branch to a commit, waiting for the user to confirm.
/// </summary>
/// <param name="Sha">The commit to reset to.</param>
/// <param name="Mode">What to reset besides the branch.</param>
public sealed record ResetRequest(string Sha, GitResetMode Mode);
