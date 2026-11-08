namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// The user's settings for a built-in tool (see <see cref="BuiltInTool"/>).
/// </summary>
public sealed record BuiltInToolSettings
{
    /// <summary>
    /// Whether the tool is offered when it's installed.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// What the tool opens, or <c>null</c> for its <see cref="BuiltInTool.DefaultScope"/>.
    /// </summary>
    public ToolScope? Scope { get; init; }
}
