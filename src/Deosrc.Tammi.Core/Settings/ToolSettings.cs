using System.Text.Json.Serialization;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Tools;

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
    /// The <see cref="ToolDefinition.Id"/>s of the predefined tools (see <see cref="PredefinedTools"/>) that the user
    /// has disabled. Every other predefined tool is enabled.
    /// </summary>
    public IReadOnlyList<string> DisabledPredefinedTools { get; init; } = [];

    /// <summary>
    /// The custom tools, in the order they're offered after the predefined ones.
    /// </summary>
    public IReadOnlyList<ToolDefinition> CustomTools { get; init; } = [];

    /// <summary>
    /// Every tool, in the order they're offered: the predefined tools, each enabled unless the user has disabled it,
    /// then the custom tools.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ToolDefinition> Tools =>
    [
        .. PredefinedTools.All.Select(tool => tool with { Enabled = IsEnabled(tool) }),
        .. CustomTools,
    ];

    /// <summary>
    /// Whether a predefined tool is enabled.
    /// </summary>
    public bool IsEnabled(ToolDefinition predefinedTool)
    {
        ArgumentNullException.ThrowIfNull(predefinedTool);
        return !DisabledPredefinedTools.Contains(predefinedTool.Id);
    }

    /// <summary>
    /// Enables or disables a predefined tool.
    /// </summary>
    public ToolSettings WithEnabled(ToolDefinition predefinedTool, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(predefinedTool);
        if (predefinedTool.Id is not { } id)
        {
            throw new ArgumentException("Only predefined tools are enabled here.", nameof(predefinedTool));
        }

        return this with
        {
            DisabledPredefinedTools = enabled
                ? DisabledPredefinedTools.Where(disabled => disabled != id).ToArray()
                : [.. DisabledPredefinedTools.Where(disabled => disabled != id), id],
        };
    }

    /// <summary>
    /// Trims the shell. A blank shell means the default shell, and its arguments are cleared, as that shell brings its
    /// own. Fills in missing values in each tool.
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
            DisabledPredefinedTools = DisabledPredefinedTools?.Where(id => !string.IsNullOrEmpty(id)).Distinct()
                .ToArray() ?? [],
            CustomTools = CustomTools?.Where(tool => tool is not null).Select(tool => tool.Normalize()).ToArray() ?? [],
        };
    }

    /// <inheritdoc />
    public bool Equals(ToolSettings? other) =>
        other is not null
        && TerminalShell == other.TerminalShell
        && (TerminalShellArguments is null || other.TerminalShellArguments is null
            ? TerminalShellArguments == other.TerminalShellArguments
            : TerminalShellArguments.SequenceEqual(other.TerminalShellArguments))
        && (DisabledPredefinedTools is null || other.DisabledPredefinedTools is null
            ? DisabledPredefinedTools == other.DisabledPredefinedTools
            : DisabledPredefinedTools.SequenceEqual(other.DisabledPredefinedTools))
        && (CustomTools is null || other.CustomTools is null
            ? CustomTools == other.CustomTools
            : CustomTools.SequenceEqual(other.CustomTools));

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(TerminalShell, TerminalShellArguments?.Count, DisabledPredefinedTools?.Count, CustomTools?.Count);
}
