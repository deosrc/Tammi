namespace Deosrc.Tammi.Core.Windowing;

/// <summary>
/// The sizes the user has given parts of the window, kept so they're the same next time. Like the window's size,
/// they're not settings.
/// </summary>
public sealed record LayoutState
{
    /// <summary>
    /// The width of a git graph's commit details panel, in CSS pixels, or <see langword="null"/> for its default. It's
    /// the width the user chose: the panel is narrower when the window doesn't have room for it.
    /// </summary>
    public int? GitGraphDetailsWidth { get; init; }

    /// <summary>
    /// The height of the commit message's body in a git graph's details, in CSS pixels, or <see langword="null"/> for
    /// its default. It's the height the user chose: the body is shorter when the panel doesn't have room for it.
    /// </summary>
    public int? GitGraphMessageHeight { get; init; }

    /// <summary>
    /// Replaces values that can't be used with the defaults.
    /// </summary>
    public LayoutState Normalize() => this with
    {
        GitGraphDetailsWidth = GitGraphDetailsWidth is > 0 ? GitGraphDetailsWidth : null,
        GitGraphMessageHeight = GitGraphMessageHeight is > 0 ? GitGraphMessageHeight : null,
    };
}
