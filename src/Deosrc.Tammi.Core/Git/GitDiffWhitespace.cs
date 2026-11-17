namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// How a diff treats whitespace changes.
/// </summary>
public enum GitDiffWhitespace
{
    /// <summary>Whitespace changes are shown like any other change.</summary>
    Show,

    /// <summary>Changes in the amount of whitespace are ignored (<c>git diff -b</c>).</summary>
    IgnoreAmount,

    /// <summary>All whitespace is ignored (<c>git diff -w</c>).</summary>
    IgnoreAll,

    /// <summary>Whitespace at the end of lines is ignored (<c>git diff --ignore-space-at-eol</c>).</summary>
    IgnoreAtEol,
}
