namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// A built-in tool found on this machine by <see cref="BuiltInToolFinder"/>.
/// </summary>
/// <param name="Tool">The tool.</param>
/// <param name="ExecutablePath">
/// The full path of the executable that starts it, or <c>null</c> for the file manager, which Tammi opens with the
/// system's default application for folders.
/// </param>
public sealed record InstalledTool(BuiltInTool Tool, string? ExecutablePath);
