using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// The Workspace View's tools: what the Workspace Folder and each repository can be opened in, and opening them.
/// </summary>
public sealed class WorkspaceTools
{
    private readonly IProcessLauncher _processLauncher;

    /// <summary>
    /// Creates the service.
    /// </summary>
    public WorkspaceTools(IProcessLauncher processLauncher)
    {
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
    }

    /// <summary>
    /// Finds a tool's executable.
    /// </summary>
    /// <returns>Its full path, or <c>null</c> if it can't be found (so the tool isn't offered).</returns>
    public string? FindExecutable(ToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        var executable = tool.ExpandedExecutable;
        return string.IsNullOrWhiteSpace(executable) ? null : _processLauncher.FindExecutable(executable);
    }

    /// <summary>
    /// What the Workspace Folder, or one of its repositories, can be opened in.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="repository">The repository, or <c>null</c> for the Workspace Folder.</param>
    /// <param name="settings">The Tools settings.</param>
    /// <returns>
    /// The Terminal tool's entry, then the entries of each enabled tool whose executable is found and whose scope includes the folder, in the order of
    /// <see cref="ToolSettings.Tools"/>. A folder tool has one entry. A file tool has one for each file it finds, in the
    /// repository or, for the Workspace Folder, in every repository. An entry whose name or arguments use a placeholder
    /// that has no value there (e.g. <c>{RepositoryFolder}</c> for the Workspace Folder) is left out.
    /// </returns>
    /// <remarks>It reads the repositories' folders to find files.</remarks>
    public IReadOnlyList<ToolEntry> GetEntries(Workspace workspace, WorkspaceRepository? repository, ToolSettings settings)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(settings);

        var entries = new List<ToolEntry>();
        foreach (var tool in settings.Tools)
        {
            if (!tool.Enabled || !ToolDefinition.Includes(tool.Scope, repository is not null))
            {
                continue;
            }

            if (tool.OpensTerminal)
            {
                var (folder, folderRepository) = PreferredFolder(workspace, repository, settings);
                entries.Add(new ToolEntry(tool, tool.Name, folder, folderRepository, null));
                continue;
            }

            if (FindExecutable(tool) is not { } executable)
            {
                continue;
            }

            entries.AddRange(tool.Kind == ToolKind.File
                ? FileEntries(tool, executable, workspace, repository, settings)
                : FolderEntries(tool, executable, workspace, repository, settings));
        }

        return entries;
    }

    /// <summary>
    /// Groups the entries of the Workspace Folder and its repositories by tool, so each tool is listed once with the
    /// levels it can be launched at.
    /// </summary>
    /// <param name="tools">The tools, in the order they're offered (see <see cref="ToolSettings.Tools"/>).</param>
    /// <param name="workspaceEntries">The Workspace Folder's entries (see <see cref="GetEntries"/>).</param>
    /// <param name="repositoryEntries">Each repository's entries, in the workspace's repository order.</param>
    /// <returns>
    /// An item for each folder tool, and for each file a file tool opens, ordered as <paramref name="tools"/> and then
    /// by file. A file's entries for the Workspace Folder and for its repository are matched by the file's path.
    /// </returns>
    public static IReadOnlyList<ToolItem> GroupByTool(
        IReadOnlyList<ToolDefinition> tools,
        IReadOnlyList<ToolEntry> workspaceEntries,
        IEnumerable<IReadOnlyList<ToolEntry>> repositoryEntries)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(workspaceEntries);
        ArgumentNullException.ThrowIfNull(repositoryEntries);

        var groups = new Dictionary<(int Tool, string? File), (ToolEntry? Workspace, List<ToolEntry> Repositories)>();
        (int, string?) Key(ToolEntry entry)
        {
            var index = IndexOf(tools, entry.Tool);
            return (index < 0 ? int.MaxValue : index, entry.Tool.Kind == ToolKind.File ? entry.Path : null);
        }

        foreach (var entry in workspaceEntries)
        {
            var key = Key(entry);
            if (!groups.ContainsKey(key))
            {
                groups[key] = (entry, []);
            }
        }

        foreach (var entry in repositoryEntries.SelectMany(entries => entries))
        {
            var key = Key(entry);
            if (!groups.TryGetValue(key, out var group))
            {
                group = (null, []);
                groups[key] = group;
            }

            group.Repositories.Add(entry);
        }

        return groups
            .OrderBy(group => group.Key.Tool)
            .ThenBy(group => group.Key.File, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var (workspace, repositories) = group.Value;
                var first = workspace ?? repositories[0];
                return new ToolItem(first.Tool, first.Label, group.Key.File, workspace, repositories);
            })
            .ToArray();
    }

    /// <summary>
    /// The placeholders' values for an entry.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="repository">
    /// The repository the entry is offered for or its file is in, or <c>null</c> for the Workspace Folder.
    /// </param>
    /// <param name="launchFolder">The folder the entry is offered for.</param>
    /// <param name="file">The file's full path, for a file tool's entry; otherwise <c>null</c>.</param>
    /// <param name="preferredFolder">
    /// The folder the tool should open (see <see cref="ToolPlaceholders.PreferredFolder"/>), or <c>null</c> for
    /// <paramref name="launchFolder"/>.
    /// </param>
    public static IReadOnlyDictionary<string, string> PlaceholderValues(
        Workspace workspace,
        WorkspaceRepository? repository,
        string launchFolder,
        string? file,
        string? preferredFolder = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(launchFolder);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ToolPlaceholders.PreferredFolder] = preferredFolder ?? launchFolder,
            [ToolPlaceholders.LaunchFolder] = launchFolder,
            [ToolPlaceholders.WorkspaceFolder] = workspace.Folder,
        };
        if (repository is not null)
        {
            values[ToolPlaceholders.RepositoryFolder] = workspace.GetWorktreePath(repository);
        }

        if (file is not null)
        {
            values[ToolPlaceholders.FileDirectory] = Path.GetDirectoryName(file) ?? string.Empty;
            values[ToolPlaceholders.FileName] = Path.GetFileName(file);
            values[ToolPlaceholders.FileNameWithoutExtension] = Path.GetFileNameWithoutExtension(file);
            values[ToolPlaceholders.FilePath] = file;
        }

        return values;
    }

    /// <summary>
    /// Opens an entry's folder or file in its tool. The tool keeps running after Tammi exits.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The entry opens a terminal, which the Workspace View opens in a tab itself (see
    /// <see cref="Terminals.WorkspaceTerminals"/>).
    /// </exception>
    /// <exception cref="FileNotFoundException">The tool's executable can't be found.</exception>
    public void Open(ToolEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var request = entry.Request
            ?? throw new InvalidOperationException($"{entry.Label} is opened in a tab by the Workspace View.");
        var executable = _processLauncher.FindExecutable(request.FileName)
            ?? throw new FileNotFoundException($"Couldn't find the executable '{request.FileName}'.", request.FileName);

        // A batch file (e.g. a launcher script on the search path) would otherwise open a console window alongside the
        // tool.
        _processLauncher.StartDetached(request with
        {
            FileName = executable,
            HideWindow = OperatingSystem.IsWindows() && IsBatchFile(executable),
        });
    }

    /// <summary>
    /// The folder a tool should open when it's launched for <paramref name="repository"/> or the Workspace Folder (see
    /// <see cref="ToolPlaceholders.PreferredFolder"/>), and the repository it is, if any.
    /// </summary>
    public static (string Folder, WorkspaceRepository? Repository) PreferredFolder(
        Workspace workspace,
        WorkspaceRepository? repository,
        ToolSettings settings)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(settings);

        if (repository is not null)
        {
            return (workspace.GetWorktreePath(repository), repository);
        }

        return workspace.Repositories is [var only] && settings.SingleRepositoryTarget == SingleRepositoryTarget.Repository
            ? (workspace.GetWorktreePath(only), only)
            : (workspace.Folder, null);
    }

    private static IEnumerable<ToolEntry> FolderEntries(
        ToolDefinition tool,
        string executable,
        Workspace workspace,
        WorkspaceRepository? repository,
        ToolSettings settings)
    {
        var launchFolder = repository is null ? workspace.Folder : workspace.GetWorktreePath(repository);
        var (preferred, preferredRepository) = PreferredFolder(workspace, repository, settings);

        // A tool that opens the preferred folder is offered for it, and starts in it.
        var opensPreferred = Uses(tool, ToolPlaceholders.PreferredFolder);
        var folder = opensPreferred ? preferred : launchFolder;
        var values = PlaceholderValues(workspace, repository, launchFolder, null, preferred);
        if (CreateEntry(tool, executable, folder, opensPreferred ? preferredRepository : repository, values, folder)
            is { } entry)
        {
            yield return entry;
        }
    }

    private static bool Uses(ToolDefinition tool, string placeholder) =>
        tool.Arguments.Append(tool.Name).SelectMany(ToolPlaceholders.FindUsed).Contains(placeholder);

    private static IEnumerable<ToolEntry> FileEntries(
        ToolDefinition tool,
        string executable,
        Workspace workspace,
        WorkspaceRepository? repository,
        ToolSettings settings)
    {
        var launchFolder = repository is null ? workspace.Folder : workspace.GetWorktreePath(repository);
        var preferred = PreferredFolder(workspace, repository, settings).Folder;
        IEnumerable<WorkspaceRepository> searched = repository is null ? workspace.Repositories : [repository];
        foreach (var fileRepository in searched)
        {
            foreach (var file in ToolFileFinder.Find(workspace.GetWorktreePath(fileRepository), tool.FilePatterns))
            {
                var values = PlaceholderValues(workspace, fileRepository, launchFolder, file, preferred);
                if (CreateEntry(tool, executable, file, fileRepository, values, Path.GetDirectoryName(file)) is { } entry)
                {
                    yield return entry;
                }
            }
        }
    }

    private static ToolEntry? CreateEntry(
        ToolDefinition tool,
        string executable,
        string path,
        WorkspaceRepository? repository,
        IReadOnlyDictionary<string, string> values,
        string? workingDirectory)
    {
        var name = string.IsNullOrWhiteSpace(tool.Name) ? "Unnamed tool" : tool.Name.Trim();
        var label = ToolPlaceholders.Expand(name, values);
        var arguments = tool.Arguments.Select(argument => ToolPlaceholders.Expand(argument, values)).ToArray();
        if (label is null || arguments.Any(argument => argument is null))
        {
            return null;
        }

        return new ToolEntry(tool, label, path, repository,
            new ProcessRequest(executable, arguments!) { WorkingDirectory = workingDirectory });
    }

    private static int IndexOf(IReadOnlyList<ToolDefinition> tools, ToolDefinition tool)
    {
        for (var i = 0; i < tools.Count; i++)
        {
            if (Equals(tools[i], tool))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsBatchFile(string path) =>
        Path.GetExtension(path).ToUpperInvariant() is ".CMD" or ".BAT";
}
