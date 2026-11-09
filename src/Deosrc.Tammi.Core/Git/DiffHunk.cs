namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A run of changed lines, with some context, in a <see cref="FileDiff"/>.
/// </summary>
/// <param name="Header">The hunk's header, e.g. <c>@@ -1,3 +1,4 @@</c> and any function context.</param>
/// <param name="Lines">The hunk's lines.</param>
public sealed record DiffHunk(string Header, IReadOnlyList<DiffLine> Lines);
