using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A request to discard unstaged changes, waiting for the user to confirm.
/// </summary>
/// <param name="Files">The files to discard, or <see langword="null"/> for every unstaged change.</param>
public sealed record DiscardRequest(IReadOnlyList<FileChange>? Files);
