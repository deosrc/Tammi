using System.Text;
using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The task context file: a markdown file in the workspace's <c>.tammi/</c> folder with its tasks' details (titles,
/// statuses, links and descriptions), for agents to read. The initial prompt tells each agent where it is.
/// </summary>
/// <remarks>
/// <see cref="WorkspaceTaskList"/> writes it each time it has read the tasks, so it follows the tasks as they change.
/// </remarks>
public static class TaskContextFile
{
    /// <summary>
    /// The file's name, in the workspace's <c>.tammi/</c> folder.
    /// </summary>
    public const string FileName = "tasks.md";

    /// <summary>
    /// The file's full path in a workspace.
    /// </summary>
    public static string PathOf(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return Path.GetFullPath(Path.Combine(workspace.MetadataFolder, FileName));
    }

    /// <summary>
    /// The file's contents for a workspace's tasks.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="tasks">
    /// Its tasks, in its order, as last read. A task that hasn't been read, or couldn't be, is listed by its key.
    /// </param>
    public static string Render(Workspace workspace, IReadOnlyList<WorkspaceTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(tasks);

        var text = new StringBuilder();
        text.Append("# Tasks\n\n");
        text.Append("The tasks of the Tammi workspace \"").Append(workspace.Name).Append("\". ");
        text.Append("Tammi rewrites this file when the tasks change, so don't edit it.\n");
        if (tasks.Count == 0)
        {
            text.Append("\nThis workspace has no tasks. It's described by its name alone.\n");
        }

        foreach (var task in tasks)
        {
            text.Append('\n');
            AppendTask(text, task);
        }

        return text.ToString();
    }

    /// <summary>
    /// Writes the file for a workspace's tasks, unless it already has those contents. The file is written in full or
    /// not at all.
    /// </summary>
    /// <exception cref="IOException">
    /// The file couldn't be written, e.g. the workspace's <c>.tammi/</c> folder doesn't exist.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">The file couldn't be written.</exception>
    public static async Task WriteAsync(
        Workspace workspace,
        IReadOnlyList<WorkspaceTask> tasks,
        CancellationToken cancellationToken = default)
    {
        var path = PathOf(workspace);
        var contents = Render(workspace, tasks);
        if (File.Exists(path)
            && await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) == contents)
        {
            return;
        }

        await WriteFileAsync(path, contents, overwrite: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the file, listing the workspace's tasks by their keys, if it doesn't exist yet. Their details are added
    /// when they're read (see <see cref="WorkspaceTaskList"/>).
    /// </summary>
    /// <returns>The file's full path.</returns>
    /// <exception cref="IOException">
    /// The file couldn't be written, e.g. the workspace's <c>.tammi/</c> folder doesn't exist.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">The file couldn't be written.</exception>
    public static async Task<string> CreateIfMissingAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default)
    {
        var path = PathOf(workspace);
        if (File.Exists(path))
        {
            return path;
        }

        var contents = Render(workspace, [.. workspace.Tasks.Select(reference => new WorkspaceTask(reference))]);
        try
        {
            // Not overwriting, so the tasks' details aren't replaced if they've just been written.
            await WriteFileAsync(path, contents, overwrite: false, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException) when (File.Exists(path))
        {
        }

        return path;
    }

    private static void AppendTask(StringBuilder text, WorkspaceTask task)
    {
        text.Append("## ").Append(task.Key);
        if (task.Task is not { } info)
        {
            text.Append("\n\n").Append(task.Problem switch
            {
                null => "Tammi hasn't read this task's details yet.",
                WorkspaceTaskProblem.SourceNotConnected =>
                    "Tammi couldn't read this task's details, because its Task Source isn't connected.",
                WorkspaceTaskProblem.NotFound =>
                    "Tammi couldn't read this task's details: it wasn't found, or the user can't see it.",
                _ => "Tammi couldn't read this task's details. " + task.ProblemDetail,
            }).Append('\n');
            return;
        }

        text.Append(": ").Append(info.Title).Append("\n\n");
        text.Append("- Link: ").Append(info.Url).Append('\n');
        if (info.Status is { } status)
        {
            text.Append("- Status: ").Append(status);
            if (info.StatusCategory is { } category)
            {
                text.Append(" (").Append(category switch
                {
                    TaskStatusCategory.ToDo => "to do",
                    TaskStatusCategory.InProgress => "in progress",
                    _ => "done",
                }).Append(')');
            }

            text.Append('\n');
        }

        if (info.RepositoryUrl is { } repository)
        {
            text.Append("- Repository: ").Append(repository).Append('\n');
        }

        text.Append("\n### Description\n\n");
        text.Append(string.IsNullOrWhiteSpace(info.Description)
            ? "No description."
            : info.Description.ReplaceLineEndings("\n").Trim('\n'));
        text.Append('\n');
    }

    private static async Task WriteFileAsync(
        string path,
        string contents,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        // Write a temporary file and move it into place, so an agent never reads a half-written file. Each write has
        // its own temporary file, so writes at the same time don't mix.
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, contents, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
