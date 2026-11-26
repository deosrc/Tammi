using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// A tool that the Workspace View offers to open the Workspace Folder, a repository or a file in. The predefined tools
/// (see <see cref="PredefinedTools"/>) are built-in definitions. Custom tools are kept in the settings file and edited
/// on the Settings screen.
/// </summary>
/// <remarks>
/// A tool is only offered when its executable is found, except the Terminal tool, which has none. Two definitions are equal when their values are, including
/// their lists.
/// </remarks>
public sealed partial record ToolDefinition
{
    /// <summary>
    /// Identifies a predefined tool in the settings file, or <c>null</c> for a custom tool. It never changes.
    /// </summary>
    [JsonIgnore]
    public string? Id { get; init; }

    /// <summary>
    /// What the tool opens.
    /// </summary>
    public ToolKind Kind { get; init; } = ToolKind.Folder;

    /// <summary>
    /// The name the tool is shown with in the Tools section, e.g. <c>Visual Studio</c>. Placeholders written in it by
    /// older versions (e.g. <c>Visual Studio - {FileName}</c>) are left out of <see cref="DisplayName"/>: a file tool's
    /// menu items are named by <see cref="MenuLabel"/> instead.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// For a file tool, what its menu item for each file found is called. Not used by folder tools, whose menu items
    /// are the Workspace Folder and the repositories.
    /// </summary>
    public ToolMenuLabel MenuLabel { get; init; } = ToolMenuLabel.FileName;

    /// <summary>
    /// For a file tool, the files it opens. A pattern with no <c>/</c> (e.g. <c>*.sln</c>) matches file names in the
    /// repository and its subfolders (see <see cref="ToolFileFinder"/>). A pattern with a <c>/</c> (e.g.
    /// <c>.devcontainer/devcontainer.json</c>) is a path from the repository's folder. <c>*</c> and <c>?</c> are
    /// wildcards, and case is ignored. Not used by folder tools.
    /// </summary>
    public IReadOnlyList<string> FilePatterns { get; init; } = [];

    /// <summary>
    /// The executable's name or path. Names are found on the search path, as a shell would find them. Environment
    /// variables written as <c>%NAME%</c> are filled in.
    /// </summary>
    public string Executable { get; init; } = string.Empty;

    /// <summary>
    /// The arguments, each passed to the tool as-is, with placeholders (see <see cref="ToolPlaceholders"/>).
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// Whether a folder tool is offered for the Workspace Folder, the repositories, or both. Not used by file tools,
    /// which open the files they find wherever they find them (see <see cref="IsOfferedFor"/>).
    /// </summary>
    public ToolScope Scope { get; init; } = ToolScope.Both;

    /// <summary>
    /// Which icon the tool is shown with.
    /// </summary>
    public ToolIconSettings Icon { get; init; } = new();

    /// <summary>
    /// Whether the tool is offered. For a predefined tool, it comes from the settings (see
    /// <see cref="Settings.ToolSettings.Tools"/>).
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Whether the tool opens one of Tammi's own terminals in the Workspace View, rather than starting its executable.
    /// Only the predefined Terminal tool does (see <see cref="PredefinedTools.Terminal"/>): it has no executable, and
    /// it's always enabled.
    /// </summary>
    [JsonIgnore]
    public bool OpensTerminal { get; init; }

    /// <summary>
    /// The name, without the placeholders and what joins them to it (e.g. <c>Visual Studio - {FileName}</c> is
    /// <c>Visual Studio</c>), trimmed; or <c>Unnamed tool</c> if that leaves nothing.
    /// </summary>
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var plain = PlaceholderInName().Replace(Name ?? string.Empty, string.Empty).Trim();
            return plain.Length > 0 ? plain : "Unnamed tool";
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s*[-\u2013\u2014:|]?\s*\{[^{}]*\}")]
    private static partial System.Text.RegularExpressions.Regex PlaceholderInName();

    /// <summary>
    /// Whether this is a predefined tool.
    /// </summary>
    [JsonIgnore]
    public bool IsPredefined => Id is not null;

    /// <summary>
    /// The executable, with environment variables filled in.
    /// </summary>
    [JsonIgnore]
    public string ExpandedExecutable => Environment.ExpandEnvironmentVariables(Executable.Trim());

    /// <summary>
    /// Replaces missing and unknown values (e.g. from a settings file written by hand) with defaults, and trims the
    /// executable.
    /// </summary>
    public ToolDefinition Normalize() => this with
    {
        Kind = Enum.IsDefined(Kind) ? Kind : ToolKind.Folder,
        Name = Name ?? string.Empty,
        MenuLabel = Enum.IsDefined(MenuLabel) ? MenuLabel : ToolMenuLabel.FileName,
        FilePatterns = FilePatterns?.Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => pattern.Trim())
            .ToArray() ?? [],
        Executable = Executable?.Trim() ?? string.Empty,
        Arguments = Arguments?.Where(argument => argument is not null).ToArray() ?? [],
        Scope = Enum.IsDefined(Scope) ? Scope : ToolScope.Both,
        Icon = (Icon ?? new ToolIconSettings()).Normalize(),
    };

    /// <summary>
    /// Whether the tool is offered for a repository (<paramref name="repository"/> is <c>true</c>) or for the Workspace
    /// Folder: a folder tool where its <see cref="Scope"/> includes it, and a file tool for both.
    /// </summary>
    public bool IsOfferedFor(bool repository) => Kind == ToolKind.File || Includes(Scope, repository);

    /// <summary>
    /// Whether a tool with <paramref name="scope"/> is offered for a repository (<paramref name="repository"/> is
    /// <c>true</c>) or for the Workspace Folder.
    /// </summary>
    public static bool Includes(ToolScope scope, bool repository) =>
        scope == ToolScope.Both || scope == (repository ? ToolScope.Repository : ToolScope.Workspace);

    /// <inheritdoc />
    public bool Equals(ToolDefinition? other) =>
        other is not null
        && Id == other.Id
        && Kind == other.Kind
        && Name == other.Name
        && MenuLabel == other.MenuLabel
        && Executable == other.Executable
        && Scope == other.Scope
        && Equals(Icon, other.Icon)
        && Enabled == other.Enabled
        && OpensTerminal == other.OpensTerminal
        && SequenceEqual(FilePatterns, other.FilePatterns)
        && SequenceEqual(Arguments, other.Arguments);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Kind, Name, Executable, Scope, Enabled, Arguments?.Count);

    private static bool SequenceEqual(IReadOnlyList<string>? first, IReadOnlyList<string>? second) =>
        first is null || second is null ? first == second : first.SequenceEqual(second);
}
