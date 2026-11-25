using Microsoft.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Components;

/// <summary>
/// A tab in a <see cref="TabBar"/>.
/// </summary>
/// <param name="Key">Identifies the tab among the bar's tabs, so it keeps its state as tabs open and close.</param>
/// <param name="Href">Where the tab goes. A tab is a link: what's shown follows the address.</param>
/// <param name="Title">The tab's tooltip, and its name for assistive technology when it has no label.</param>
/// <param name="Label">The tab's text. Without one the tab is only its icon.</param>
/// <param name="Icon">The icon before the label.</param>
/// <param name="Indicator">The status indicator after the label, if the tab has one: a dot, a progress spinner or nothing.</param>
/// <param name="Active">Whether the tab is the one shown.</param>
public sealed record TabBarItem(
    string Key,
    string Href,
    string Title,
    string? Label = null,
    RenderFragment? Icon = null,
    RenderFragment? Indicator = null,
    bool Active = false);
