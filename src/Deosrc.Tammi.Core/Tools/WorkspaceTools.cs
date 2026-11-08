using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tools;

/// The Workspace View's tools: which built-in tools are installed, what each workspace and repository can be
/// opened in, and opening them.
/// opening them.
/// </summary>
public sealed class WorkspaceTools
{
    private readonly IProcessLauncher _processLauncher;
    private readonly BuiltInToolFinder _finder;
    private readonly Lock _lock = new();
    private Task<IReadOnlyList<InstalledTool>>? _installed;

    /// <summary>
    /// Creates the service.
    /// </summary>
    public WorkspaceTools(IProcessLauncher processLauncher, BuiltInToolFinder finder)
    {
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        _finder = finder ?? throw new ArgumentNullException(nameof(finder));
    }

    /// <summary>
    /// Raised when the installed tools have been looked for again (see <see cref="RefreshInstalledAsync"/>).
    /// </summary>
    public event Action? InstalledChanged;

    /// <summary>
    /// The installed built-in tools. They're looked for the first time they're needed, and the result is kept until
    /// <see cref="RefreshInstalledAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<InstalledTool>> GetInstalledAsync()
    {
        lock (_lock)
        {
            return _installed ??= FindInstalledAsync();
        }
    }

    /// <summary>
    /// Looks for the installed built-in tools again, e.g. after the user has installed one.
    /// </summary>
    public async Task<IReadOnlyList<InstalledTool>> RefreshInstalledAsync()
    {
        Task<IReadOnlyList<InstalledTool>> installed;
        lock (_lock)
        {
            installed = _installed = FindInstalledAsync();
        }

        var result = await installed.ConfigureAwait(false);
        InstalledChanged?.Invoke();
        return result;
    }

    /// <summary>
    /// What a workspace's Workspace Folder, or one of its repositories, can be opened in.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="repository">The repository, or <c>null</c> for the Workspace Folder.</param>
    /// <param name="settings">The Tools settings.</param>
    /// <param name="installed">The installed built-in tools (see <see cref="GetInstalledAsync"/>).</param>
    /// <returns>
    /// The entries of the enabled built-in tools that are installed, then of the enabled custom tools, each in their
    /// order, for the tools whose scope includes the folder. A tool that opens solutions has an entry for each solution
    /// in the folder (for the Workspace Folder, in each of its repositories), and none if there aren't any. The dev
    /// container tool only has an entry when the folder has a dev container configuration.
    /// </returns>
    /// <remarks>It reads the folders, to find solutions and dev container configurations.</remarks>
    public static IReadOnlyList<ToolEntry> GetEntries(
        Workspace workspace,
        WorkspaceRepository? repository,
        ToolSettings settings,
        IReadOnlyList<InstalledTool> installed)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(installed);

        var isRepository = repository is not null;
        var folder = repository is null ? workspace.Folder : workspace.GetWorktreePath(repository);
        var entries = new List<ToolEntry>();

        foreach (var (tool, executable) in installed)
        {
            if (!settings.GetBuiltIn(tool).Enabled || !ToolDefinition.Includes(settings.GetScope(tool), isRepository))
            {
                continue;
            }

            switch (tool.Kind)
            {
                case BuiltInToolKind.Terminal:
                    entries.Add(new ToolEntry(tool.Name, tool.Icon, ToolEntryKind.Terminal, folder, repository, null));
                    break;
                case BuiltInToolKind.FileManager:
                    entries.Add(new ToolEntry(
                        tool.Name, tool.Icon, ToolEntryKind.SystemDefault, folder, repository, null));
                    break;
                case BuiltInToolKind.Folder when executable is not null:
                    entries.Add(ProcessEntry(tool.Name, tool.Icon, folder, repository, executable, folder));
                    break;
                case BuiltInToolKind.DevContainer when executable is not null:
                    if (DevContainerConfig.Find(folder) is { } config)
                    {
                        entries.Add(ProcessEntry(
                            tool.Name, tool.Icon, folder, repository, executable,
                            "--folder-uri", DevContainerConfig.BuildFolderUri(folder, config.ContainerFolder)));
                    }

                    break;
                case BuiltInToolKind.Solution when executable is not null:
                    entries.AddRange(SolutionEntries(tool, executable, workspace, repository));
                    break;
            }
        }

        foreach (var tool in settings.CustomTools)
        {
            if (tool.Enabled && ToolDefinition.Includes(tool.Scope, isRepository))
            {
                var name = string.IsNullOrWhiteSpace(tool.Name) ? "Unnamed tool" : tool.Name.Trim();
                entries.Add(new ToolEntry(
                    name, tool.Icon, ToolEntryKind.Process, folder, repository,
                    new ProcessRequest(tool.Executable, tool.BuildArguments(folder)) { WorkingDirectory = folder }));
            }
        }

        return entries;
    }

    /// <summary>
    /// Opens an entry's folder or solution in its tool. The tool keeps running after Tammi exits.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The entry opens a terminal, which the Workspace View starts itself (see
    /// <see cref="Terminals.WorkspaceTerminals"/>).
    /// </exception>
    /// <exception cref="FileNotFoundException">The tool's executable can't be found.</exception>
    public void Open(ToolEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        switch (entry.Kind)
        {
            case ToolEntryKind.SystemDefault:
                _processLauncher.Open(entry.Path);
                break;
            case ToolEntryKind.Process when entry.Request is { } request:
                if (string.IsNullOrWhiteSpace(request.FileName))
                {
                    throw new FileNotFoundException($"{entry.Label} has no executable. Set one in Settings.");
                }

                var executable = _processLauncher.FindExecutable(request.FileName)
                    ?? throw new FileNotFoundException(
                        $"Couldn't find the executable '{request.FileName}'.", request.FileName);

                // A batch file (e.g. a launcher script on the search path) would otherwise open a console window
                // alongside the tool.
                _processLauncher.StartDetached(request with
                {
                    FileName = executable,
                    HideWindow = OperatingSystem.IsWindows() && IsBatchFile(executable),
                });
                break;
            default:
                throw new InvalidOperationException($"{entry.Label} is opened by the Workspace View.");
        }
    }

    private Task<IReadOnlyList<InstalledTool>> FindInstalledAsync() => Task.Run(() => _finder.FindAsync());

    private static ToolEntry ProcessEntry(
        string label,
        ToolIcon icon,
        string path,
        WorkspaceRepository? repository,
        string executable,
        params string[] arguments) =>
        new(label, icon, ToolEntryKind.Process, path, repository,
            new ProcessRequest(executable, arguments)
            {
                // A solution's tool starts in the solution's folder.
                WorkingDirectory = File.Exists(path) ? Path.GetDirectoryName(path) : path,
            });

    private static IEnumerable<ToolEntry> SolutionEntries(
        BuiltInTool tool,
        string executable,
        Workspace workspace,
        WorkspaceRepository? repository)
    {
        IEnumerable<WorkspaceRepository> repositories = repository is null ? workspace.Repositories : [repository];
        var solutions = repositories
            .SelectMany(r => SolutionFinder.Find(workspace.GetWorktreePath(r)).Select(path => (Repository: r, Path: path)))
            .ToArray();

        // Solutions are named by their file, unless another has the same name: they're then told apart by their path
        // in the folder being opened.
        var root = repository is null ? workspace.Folder : workspace.GetWorktreePath(repository);
        foreach (var (solutionRepository, path) in solutions)
        {
            var fileName = Path.GetFileName(path);
            var name = solutions.Count(other =>
                    Path.GetFileName(other.Path).Equals(fileName, StringComparison.OrdinalIgnoreCase)) > 1
                ? Path.GetRelativePath(root, path).Replace('\\', '/')
                : fileName;
            yield return ProcessEntry($"{tool.Name}: {name}", tool.Icon, path, solutionRepository, executable, path);
        }
    }

    private static bool IsBatchFile(string path) =>
        Path.GetExtension(path).ToUpperInvariant() is ".CMD" or ".BAT";
}
