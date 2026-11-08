namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A repository's HEAD, branches and tags, from <see cref="IGitRepositoryReader.ReadRefs"/>.
/// </summary>
public sealed record GitRefs
{
    /// <summary>
    /// A repository with no commits or references.
    /// </summary>
    public static GitRefs Empty { get; } = new();

    /// <summary>
    /// The commit HEAD points to, or <see langword="null"/> when there are no commits yet.
    /// </summary>
    public string? HeadSha { get; init; }

    /// <summary>
    /// The local branch HEAD is on, or <see langword="null"/> when HEAD is detached.
    /// </summary>
    public string? HeadBranch { get; init; }

    /// <summary>
    /// The local branches, then the remote-tracking branches, then the tags, each in name order. Symbolic references
    /// such as <c>origin/HEAD</c> aren't included.
    /// </summary>
    public IReadOnlyList<GitReference> References { get; init; } = [];

    /// <summary>
    /// Whether <paramref name="other"/> holds the same refs, comparing the references item by item.
    /// </summary>
    public bool Equals(GitRefs? other) =>
        other is not null
        && HeadSha == other.HeadSha
        && HeadBranch == other.HeadBranch
        && References.SequenceEqual(other.References);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(HeadSha, HeadBranch, References.Count);
}
