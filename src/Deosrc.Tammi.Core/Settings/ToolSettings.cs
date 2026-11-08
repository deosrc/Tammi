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
    /// The user's settings for the built-in tools, by <see cref="BuiltInTool.Id"/>. A tool that isn't here
    /// has the default settings: it's enabled, with its default scope.
    /// </summary>
    public IReadOnlyDictionary<string, BuiltInToolSettings> BuiltInTools { get; init; } =
        new Dictionary<string, BuiltInToolSettings>();

    /// <summary>
    /// The custom tools, in the order they're offered after the built-in ones.
    /// </summary>
    public IReadOnlyList<ToolDefinition> CustomTools { get; init; } = [];

    /// <summary>
    /// The user's settings for a built-in tool.
    /// </summary>
    public BuiltInToolSettings GetBuiltIn(BuiltInTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return BuiltInTools.TryGetValue(tool.Id, out var settings) ? settings : new BuiltInToolSettings();
    }

    /// <summary>
    /// What a built-in tool opens: the user's choice, or else its default scope.
    /// </summary>
    public ToolScope GetScope(BuiltInTool tool) => GetBuiltIn(tool).Scope ?? tool.DefaultScope;

    /// <summary>
    /// Changes the settings for a built-in tool.
    /// </summary>
    public ToolSettings WithBuiltIn(BuiltInTool tool, Func<BuiltInToolSettings, BuiltInToolSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var builtInTools = new Dictionary<string, BuiltInToolSettings>(BuiltInTools)
        {
            [tool.Id] = update(GetBuiltIn(tool)),
        };
        return this with { BuiltInTools = builtInTools };
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
            BuiltInTools = BuiltInTools?.Where(entry => entry.Value is not null)
                .ToDictionary(entry => entry.Key, entry => entry.Value with
                {
                    Scope = entry.Value.Scope is { } scope && Enum.IsDefined(scope) ? scope : null,
                }) ?? new Dictionary<string, BuiltInToolSettings>(),
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
        && (BuiltInTools is null || other.BuiltInTools is null
            ? BuiltInTools == other.BuiltInTools
            : BuiltInTools.Count == other.BuiltInTools.Count
                && BuiltInTools.All(entry =>
                    other.BuiltInTools.TryGetValue(entry.Key, out var value) && Equals(entry.Value, value)))
        && (CustomTools is null || other.CustomTools is null
            ? CustomTools == other.CustomTools
            : CustomTools.SequenceEqual(other.CustomTools));

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(TerminalShell, TerminalShellArguments?.Count, BuiltInTools?.Count, CustomTools?.Count);
}
