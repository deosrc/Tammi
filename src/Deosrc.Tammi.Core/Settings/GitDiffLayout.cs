namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// How a file's diff is laid out.
/// </summary>
public enum GitDiffLayout
{
    /// <summary>One column, with removed lines above the added lines that replace them.</summary>
    Unified,

    /// <summary>Two columns: the old version on the left and the new version on the right.</summary>
    SideBySide,
}
