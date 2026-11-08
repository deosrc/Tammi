namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// Which icon a tool is shown with.
/// </summary>
public sealed record ToolIconSettings
{
    /// <summary>
    /// Where the icon comes from.
    /// </summary>
    public ToolIconSource Source { get; init; } = ToolIconSource.Application;

    /// <summary>
    /// The generic icon: shown when <see cref="Source"/> is <see cref="ToolIconSource.Generic"/>, and in place of the
    /// application's or custom icon when that can't be found.
    /// </summary>
    public ToolIcon Generic { get; init; } = ToolIcon.Application;

    /// <summary>
    /// The full path of the image file shown when <see cref="Source"/> is <see cref="ToolIconSource.Custom"/>.
    /// </summary>
    public string? CustomPath { get; init; }

    /// <summary>
    /// Replaces unknown values (e.g. from a settings file written by hand) with the defaults, and trims the path.
    /// </summary>
    public ToolIconSettings Normalize() => this with
    {
        Source = Enum.IsDefined(Source) ? Source : ToolIconSource.Application,
        Generic = Enum.IsDefined(Generic) ? Generic : ToolIcon.Application,
        CustomPath = string.IsNullOrWhiteSpace(CustomPath) ? null : CustomPath.Trim(),
    };
}
