namespace Deosrc.Tammi.UI.Components;

/// <summary>
/// A tab the user has moved in a <see cref="TabBar"/>, by dragging it or from the keyboard.
/// </summary>
/// <param name="Item">The tab moved, one of the bar's closable tabs.</param>
/// <param name="Index">Its new place among the closable tabs, counted without it.</param>
public sealed record TabBarMove(TabBarItem Item, int Index);
