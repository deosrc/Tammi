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
    /// The placeholders' values for an entry.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="repository">
    /// The repository the entry is offered for or its file is in, or <c>null</c> for the Workspace Folder.
    /// </param>
    /// <param name="launchFolder">The folder the entry is offered for.</param>
    /// <param name="file">The file's full path, for a file tool's entry; otherwise <c>null</c>.</param>
    public static IReadOnlyDictionary<string, string> PlaceholderValues(
        Workspace workspace,
        WorkspaceRepository? repository,
        string launchFolder,
        string? file)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(launchFolder);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
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

    private static IEnumerable<ToolEntry> FolderEntries(
        ToolDefinition tool,
        string executable,
        Workspace workspace,
        WorkspaceRepository? repository)
    {
        var folder = repository is null ? workspace.Folder : workspace.GetWorktreePath(repository);
        if (CreateEntry(tool, executable, folder, repository,
                PlaceholderValues(workspace, repository, folder, null), folder) is { } entry)
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
                var values = PlaceholderValues(workspace, fileRepository, launchFolder, file);
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

    private static bool IsBatchFile(string path) =>
        Path.GetExtension(path).ToUpperInvariant() is ".CMD" or ".BAT";
}
