namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// A custom tool: one the user has added on the Settings screen. It's offered whenever it's enabled, even if
/// its executable can't be found.
/// </summary>
/// <remarks>
/// Two definitions are equal when their values are, including their arguments.
/// </remarks>
public sealed record ToolDefinition
{
    /// <summary>
    /// The placeholder in <see cref="Arguments"/> for the full path of the folder being opened.
    /// </summary>
    public const string PathPlaceholder = "{path}";

    /// <summary>
    /// The name shown to the user.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The icon it's shown with.
    /// </summary>
    public ToolIcon Icon { get; init; } = ToolIcon.Application;

    /// <summary>
    /// The executable's name or path. Names are found on the search path, as a shell would find them.
    /// </summary>
    public string Executable { get; init; } = string.Empty;

    /// <summary>
    /// The arguments, each passed to the tool as-is, with <see cref="PathPlaceholder"/> where the folder's path goes.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [PathPlaceholder];

    /// <summary>
    /// Whether the tool is offered.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// What the tool opens.
    /// </summary>
    public ToolScope Scope { get; init; } = ToolScope.Both;

    /// <summary>
    /// Whether any argument has <see cref="PathPlaceholder"/>, so the tool is told which folder to open.
    /// </summary>
    public bool HasPathPlaceholder =>
        Arguments.Any(argument => argument.Contains(PathPlaceholder, StringComparison.Ordinal));

    /// <summary>
    /// The arguments to open a folder with: <see cref="Arguments"/>, with the folder's path in place of every
    /// <see cref="PathPlaceholder"/>.
    /// </summary>
    /// <remarks>
    /// Each argument is passed to the tool as-is, so a path with spaces needs no quoting. Arguments without the
    /// placeholder are passed unchanged, and the path isn't added if none has it.
    /// </remarks>
    public IReadOnlyList<string> BuildArguments(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ExpandPath(Arguments, path);
    }

    /// <summary>
    /// Replaces <see cref="PathPlaceholder"/> with <paramref name="path"/> in each argument.
    /// </summary>
    public static IReadOnlyList<string> ExpandPath(IEnumerable<string> arguments, string path)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(path);
        return arguments.Select(argument => argument.Replace(PathPlaceholder, path, StringComparison.Ordinal)).ToArray();
    }

    /// <summary>
    /// Replaces missing values (e.g. from a settings file written by hand) with empty ones, and trims the executable.
    /// </summary>
    public ToolDefinition Normalize() => this with
    {
        Name = Name ?? string.Empty,
        Executable = Executable?.Trim() ?? string.Empty,
        Arguments = Arguments?.Where(argument => argument is not null).ToArray() ?? [],
        Icon = Enum.IsDefined(Icon) ? Icon : ToolIcon.Application,
        Scope = Enum.IsDefined(Scope) ? Scope : ToolScope.Both,
    };

    /// <summary>
    /// Whether a tool with <paramref name="scope"/> is offered for a repository (<paramref name="repository"/> is
    /// <c>true</c>) or for the Workspace Folder.
    /// </summary>
    public static bool Includes(ToolScope scope, bool repository) =>
        scope == ToolScope.Both || scope == (repository ? ToolScope.Repository : ToolScope.Workspace);

    /// <inheritdoc />
    public bool Equals(ToolDefinition? other) =>
        other is not null
        && Name == other.Name
        && Icon == other.Icon
        && Executable == other.Executable
        && Enabled == other.Enabled
        && Scope == other.Scope
        && (Arguments is null || other.Arguments is null
            ? Arguments == other.Arguments
            : Arguments.SequenceEqual(other.Arguments));

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, Icon, Executable, Enabled, Scope, Arguments?.Count);
}
