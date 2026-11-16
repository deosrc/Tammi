using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// One badge in the git graph: a branch or tag, plus any remote-tracking branches of the same name on the same commit.
/// </summary>
/// <param name="Primary">The local branch, or the only reference when nothing is combined.</param>
/// <param name="Remotes">Remote-tracking branches combined into this badge when <paramref name="Primary"/> is local.</param>
public sealed record GitReferenceBadge(GitReference Primary, IReadOnlyList<GitReference> Remotes);
