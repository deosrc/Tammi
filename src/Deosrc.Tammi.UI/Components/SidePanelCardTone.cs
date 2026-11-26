using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Components;

/// <summary>
/// What a <see cref="SidePanelCard"/>'s colour says about it.
/// </summary>
public enum SidePanelCardTone
{
    /// <summary>
    /// Nothing in particular: a clean repository, or a task that isn't started.
    /// </summary>
    Neutral,

    /// <summary>
    /// Something in progress.
    /// </summary>
    Info,

    /// <summary>
    /// Something finished.
    /// </summary>
    Success,

    /// <summary>
    /// It needs attention, e.g. unpushed work.
    /// </summary>
    Warning,

    /// <summary>
    /// It's broken or can't be read.
    /// </summary>
    Danger,
}

/// <summary>
/// Extensions for <see cref="SidePanelCardTone"/>.
/// </summary>
public static class SidePanelCardToneExtensions
{
    /// <summary>
    /// The colour of a badge that goes with the tone.
    /// </summary>
    public static BadgeColor ToBadgeColor(this SidePanelCardTone tone) => tone switch
    {
        SidePanelCardTone.Info => BadgeColor.Informative,
        SidePanelCardTone.Success => BadgeColor.Success,
        SidePanelCardTone.Warning => BadgeColor.Warning,
        SidePanelCardTone.Danger => BadgeColor.Danger,
        _ => BadgeColor.Subtle,
    };
}
