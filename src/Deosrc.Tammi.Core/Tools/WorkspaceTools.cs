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
            if (!tool.Enabled || !tool.IsOfferedFor(repository is not null))
            {
                continue;
            }

            if (tool.OpensTerminal)
            {
                var folder = repository is null ? workspace.Folder : workspace.GetWorktreePath(repository);
                entries.Add(new ToolEntry(tool, tool.Name, folder, repository, null));
                continue;
            }

            if (FindExecutable(tool) is not { } executable)
            {
                continue;
            }

            entries.AddRange(tool.Kind == ToolKind.File
                ? FileEntries(tool, executable, workspace, repository)
                : FolderEntries(tool, executable, workspace, repository));
        }

        return entries;
    }

    /// <summary>
    /// What a Local Repository outside any workspace can be opened in: the Terminal tool's entry, then the entries of
    /// each enabled tool whose executable is found and that is offered for a repository, in the order of
    /// <see cref="ToolSettings.Tools"/>.
    /// </summary>
    /// <param name="repositoryPath">The full path of the repository's working directory.</param>
    /// <param name="settings">The Tools settings.</param>
    /// <remarks>It reads the repository's folders to find files.</remarks>
    public IReadOnlyList<ToolEntry> GetEntriesForRepository(string repositoryPath, ToolSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(settings);

        var workspace = StandaloneRepository.WorkspaceOf(repositoryPath);
        return GetEntries(workspace, workspace.Repositories[0], settings);
    }

    /// <summary>
    /// Groups the entries of the Workspace Folder and its repositories by tool, so each tool is listed once, whatever its
    /// kind, with the places it can be launched.
    /// </summary>
    /// <param name="tools">The tools, in the order they're offered (see <see cref="ToolSettings.Tools"/>).</param>
    /// <param name="workspaceEntries">The Workspace Folder's entries (see <see cref="GetEntries"/>).</param>
    /// <param name="repositoryEntries">Each repository's entries, in the workspace's repository order.</param>
    /// <param name="singleRepositoryTarget">
    /// What a folder tool's launch opens in a workspace with one repository (see <see cref="ToolItem.DefaultEntry"/>).
    /// </param>
    /// <returns>An item for each tool that has an entry, ordered as <paramref name="tools"/>.</returns>
    public static IReadOnlyList<ToolItem> GroupByTool(
        IReadOnlyList<ToolDefinition> tools,
        IReadOnlyList<ToolEntry> workspaceEntries,
        IEnumerable<IReadOnlyList<ToolEntry>> repositoryEntries,
        SingleRepositoryTarget singleRepositoryTarget = SingleRepositoryTarget.Repository)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(workspaceEntries);
        ArgumentNullException.ThrowIfNull(repositoryEntries);

        // A file tool's Workspace Folder entries are for the files of every repository, so they aren't a place of their
        // own: they're the same files as the repositories' entries.
        var groups = new Dictionary<int, (List<ToolEntry> Workspace, List<ToolEntry> Repositories)>();
        (List<ToolEntry> Workspace, List<ToolEntry> Repositories) GroupOf(ToolEntry entry)
        {
            var index = IndexOf(tools, entry.Tool);
            var key = index < 0 ? int.MaxValue : index;
            if (!groups.TryGetValue(key, out var group))
            {
                group = ([], []);
                groups[key] = group;
            }

            return group;
        }

        foreach (var entry in workspaceEntries)
        {
            GroupOf(entry).Workspace.Add(entry);
        }

        foreach (var entry in repositoryEntries.SelectMany(entries => entries))
        {
            GroupOf(entry).Repositories.Add(entry);
        }

        return groups
            .OrderBy(group => group.Key)
            .Select(group => CreateItem(group.Value.Workspace, group.Value.Repositories, singleRepositoryTarget))
            .ToArray();
    }

    private static ToolItem CreateItem(
        List<ToolEntry> workspace,
        List<ToolEntry> repositories,
        SingleRepositoryTarget singleRepositoryTarget)
    {
        var first = workspace.Count > 0 ? workspace[0] : repositories[0];
        var isFile = first.Tool.Kind == ToolKind.File;

        // A file tool's entries are for the files found, whichever repository they're in, so none is the Workspace
        // Folder's: the entries from the Workspace Folder, which cover every repository, are used if there are any.
        var item = isFile
            ? new ToolItem(first.Tool, first.Label, null, workspace.Count > 0 ? workspace : repositories)
            : new ToolItem(first.Tool, first.Label, workspace.Count > 0 ? first : null, repositories);

        var places = item.Places;
        var entry = places switch
        {
            [var only] => only,
            _ when !isFile && item.WorkspaceEntry is { } workspaceEntry && item.RepositoryEntries is [var repositoryEntry]
                => singleRepositoryTarget == SingleRepositoryTarget.Repository ? repositoryEntry : workspaceEntry,
            _ => null,
        };
        return item with { DefaultEntry = entry };
    }

    /// <summary>
    /// The placeholders' values for an entry.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="repository">
    /// The repository the entry is offered for or its file is in, or <c>null</c> for the Workspace Folder.
    /// </param>
    /// <param name="launchFolder">The folder the entry is offered for.</param>
    /// <param name="file">The file found, for a file tool's entry; otherwise <c>null</c>.</param>
    /// <param name="preferredFolder">
    /// The folder the tool should open (see <see cref="ToolPlaceholders.PreferredFolder"/>), or <c>null</c> for
    /// <paramref name="launchFolder"/>.
    /// </param>
    public static IReadOnlyDictionary<string, string> PlaceholderValues(
        Workspace workspace,
        WorkspaceRepository? repository,
        string launchFolder,
        ToolFileMatch? file,
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
            var path = file.FilePath;
            values[ToolPlaceholders.FileDirectory] = Path.GetDirectoryName(path) ?? string.Empty;
            values[ToolPlaceholders.FileName] = Path.GetFileName(path);
            values[ToolPlaceholders.FileNameWithoutExtension] = Path.GetFileNameWithoutExtension(path);
            values[ToolPlaceholders.FilePath] = path;
            values[ToolPlaceholders.MatchFolder] = file.MatchFolder;
            values[ToolPlaceholders.MatchFolderName] =
                Path.GetFileName(Path.TrimEndingDirectorySeparator(file.MatchFolder));
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

    private static IEnumerable<ToolEntry> FolderEntries(
        ToolDefinition tool,
        string executable,
        Workspace workspace,
        WorkspaceRepository? repository)
    {
        var launchFolder = repository is null ? workspace.Folder : workspace.GetWorktreePath(repository);
        var values = PlaceholderValues(workspace, repository, launchFolder, null);
        if (CreateEntry(tool, executable, launchFolder, repository, values, launchFolder) is { } entry)
        {
            yield return entry;
        }
    }

    private static IEnumerable<ToolEntry> FileEntries(
        ToolDefinition tool,
        string executable,
        Workspace workspace,
        WorkspaceRepository? repository)
    {
        var launchFolder = repository is null ? workspace.Folder : workspace.GetWorktreePath(repository);
        IEnumerable<WorkspaceRepository> searched = repository is null ? workspace.Repositories : [repository];
        foreach (var fileRepository in searched)
        {
            foreach (var file in ToolFileFinder.Find(workspace.GetWorktreePath(fileRepository), tool.FilePatterns))
            {
                // It starts in the folder its pattern matched from: the file's folder, or e.g. the project folder that
                // holds .devcontainer/.
                var values = PlaceholderValues(workspace, fileRepository, launchFolder, file);
                if (CreateEntry(tool, executable, file.FilePath, fileRepository, values, file.MatchFolder, file) is { } entry)
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
        string? workingDirectory,
        ToolFileMatch? file = null)
    {
        var arguments = tool.Arguments.Select(argument => ToolPlaceholders.Expand(argument, values)).ToArray();
        if (arguments.Any(argument => argument is null))
        {
            return null;
        }

        return new ToolEntry(tool, tool.DisplayName, path, repository,
            new ProcessRequest(executable, arguments!) { WorkingDirectory = workingDirectory })
        {
            MenuLabel = file is null ? null : MenuLabelOf(tool.MenuLabel, file),
        };
    }

    private static string MenuLabelOf(ToolMenuLabel label, ToolFileMatch file) => label switch
    {
        ToolMenuLabel.FileNameWithoutExtension => Path.GetFileNameWithoutExtension(file.FilePath),
        ToolMenuLabel.LaunchFolder => Path.GetFileName(Path.TrimEndingDirectorySeparator(file.MatchFolder)),
        ToolMenuLabel.FullPath => file.FilePath,
        _ => Path.GetFileName(file.FilePath),
    };

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
