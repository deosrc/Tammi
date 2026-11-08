namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// What a <see cref="GitReference"/> is.
/// </summary>
public enum GitReferenceKind
{
    /// <summary>A local branch, e.g. <c>main</c>.</summary>
    LocalBranch,

    /// <summary>A remote-tracking branch, e.g. <c>origin/main</c>.</summary>
    RemoteBranch,

    /// <summary>A tag, lightweight or annotated.</summary>
    Tag,
}
