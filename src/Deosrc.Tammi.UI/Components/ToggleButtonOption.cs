using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Components;

/// <summary>
/// One button of a <see cref="ToggleButtonGroup{TValue}"/>.
/// </summary>
/// <typeparam name="TValue">The type of the group's value.</typeparam>
/// <param name="Value">What the group's value is when this button is pressed.</param>
/// <param name="Icon">The button's icon. The button has no text.</param>
/// <param name="Title">The button's tooltip, and its name for assistive technology.</param>
public sealed record ToggleButtonOption<TValue>(TValue Value, Icon Icon, string Title);
