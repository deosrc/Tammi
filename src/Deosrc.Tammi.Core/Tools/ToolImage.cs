namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// The icon a tool is shown with, as found by <see cref="ToolImages"/>.
/// </summary>
/// <param name="DataUri">
/// The application's or custom icon, as a <c>data:</c> URI, or <c>null</c> to show <paramref name="Generic"/>.
/// </param>
/// <param name="Generic">The generic icon, shown when there's no <paramref name="DataUri"/>.</param>
public sealed record ToolImage(string? DataUri, ToolIcon Generic);
