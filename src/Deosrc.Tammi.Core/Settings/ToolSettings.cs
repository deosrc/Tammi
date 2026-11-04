using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// The Tools section of the Settings screen.
/// </summary>
public sealed record ToolSettings
{
    /// <summary>
    /// The shell that terminals run: an executable's name or path, or <c>null</c> for the platform's default shell
    /// (see <see cref="IPtyService.GetDefaultShell"/>).
    /// </summary>
    public string? TerminalShell { get; init; }

    /// <summary>
    /// The arguments for <see cref="TerminalShell"/>, each passed to it as-is. Empty when the default shell is used,
    /// which brings its own arguments.
    /// </summary>
    public IReadOnlyList<string> TerminalShellArguments { get; init; } = [];

    /// <summary>
    /// Trims the shell. A blank shell means the default shell, and its arguments are cleared, as that shell brings its
    /// own.
    /// </summary>
    public ToolSettings Normalize()
    {
        var shell = string.IsNullOrWhiteSpace(TerminalShell) ? null : TerminalShell.Trim();
        return this with
        {
            TerminalShell = shell,
            TerminalShellArguments = shell is null
                ? []
                : TerminalShellArguments?.Where(argument => argument is not null).ToArray() ?? [],
        };
    }

    /// <inheritdoc />
    public bool Equals(ToolSettings? other) =>
        other is not null
        && TerminalShell == other.TerminalShell
        && (TerminalShellArguments is null || other.TerminalShellArguments is null
            ? TerminalShellArguments == other.TerminalShellArguments
            : TerminalShellArguments.SequenceEqual(other.TerminalShellArguments));

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(TerminalShell, TerminalShellArguments?.Count);
}
