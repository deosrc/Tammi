namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// The remote branch a local branch tracks.
/// </summary>
/// <param name="Remote">The remote's name, e.g. <c>origin</c>.</param>
/// <param name="Branch">The branch's name on the remote, e.g. <c>feature</c>.</param>
public sealed record GitUpstream(string Remote, string Branch);
