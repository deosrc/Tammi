namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A branch or tag, and the commit it points to.
/// </summary>
/// <param name="Name">The short name, e.g. <c>main</c>, <c>origin/main</c> or <c>v1.0</c>.</param>
/// <param name="Kind">Whether it's a local branch, a remote-tracking branch or a tag.</param>
/// <param name="TargetSha">The commit it points to. An annotated tag's commit, not the tag object.</param>
public sealed record GitReference(string Name, GitReferenceKind Kind, string TargetSha);
