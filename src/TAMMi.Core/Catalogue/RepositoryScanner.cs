using TAMMi.Core.Platform;
using TAMMi.Core.Workspaces;

namespace TAMMi.Core.Catalogue;

/// <summary>
/// Finds the git repositories in a Monitored Folder.
/// </summary>
/// <remarks>
/// <para>
/// A folder is a repository when it has a <c>.git</c> folder, or a <c>.git</c> file that doesn't belong to a linked
/// worktree (e.g. a clone made with <c>--separate-git-dir</c>). The scan doesn't look inside repositories, so their
/// submodules and any repositories nested in them aren't listed on their own.
/// </para>
/// <para>
/// The scan also skips linked worktrees, Workspace Folders (which only hold worktrees), hidden and system folders,
/// and folders it can't read. It looks at most <see cref="MaxDepth"/> folders down, and doesn't follow links to other
/// folders, so it can't loop.
/// </para>
/// </remarks>
public static class RepositoryScanner
{
    /// <summary>
    /// How many folders below the Monitored Folder the scan looks, e.g. 3 finds
    /// <c>&lt;folder&gt;/github/acme/api</c>.
    /// </summary>
    public const int MaxDepth = 20;

    private const string GitName = ".git";

    // The file git puts in a linked worktree's git folder, pointing at the main repository's git folder.
    private const string CommonDirFileName = "commondir";

    private static readonly EnumerationOptions SubfolderOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
    };

    /// <summary>
    /// Says whether <paramref name="folder"/> is a repository's working directory.
    /// </summary>
    public static RepositoryFolderKind Inspect(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        string git = Path.Combine(folder, GitName);
        if (Directory.Exists(git))
        {
            return RepositoryFolderKind.Repository;
        }

        if (File.Exists(git))
        {
            return IsLinkedWorktree(git) ? RepositoryFolderKind.LinkedWorktree : RepositoryFolderKind.Repository;
        }

        return RepositoryFolderKind.NotRepository;
    }

    /// <summary>
    /// Lists the repositories in <paramref name="folder"/> and its subfolders, in path order. If
    /// <paramref name="folder"/> is itself a repository, it is the only one listed.
    /// </summary>
    /// <param name="folder">The folder to scan. If it doesn't exist, nothing is listed.</param>
    /// <param name="cancellationToken">Stops the scan.</param>
    /// <returns>The repositories' full paths.</returns>
    public static IReadOnlyList<string> FindRepositories(string folder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var repositories = new List<string>();
        var pending = new Stack<(DirectoryInfo Folder, int Depth)>();
        pending.Push((new DirectoryInfo(FileSystemPaths.Normalize(folder)), 0));

        while (pending.TryPop(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (current, depth) = item;

            try
            {
                if (!current.Exists)
                {
                    continue;
                }

                switch (Inspect(current.FullName))
                {
                    case RepositoryFolderKind.Repository:
                        repositories.Add(FileSystemPaths.Normalize(current.FullName));
                        continue;
                    case RepositoryFolderKind.LinkedWorktree:
                        continue;
                }

                if (depth >= MaxDepth
                    || (depth > 0 && current.LinkTarget is not null)
                    || Directory.Exists(Path.Combine(current.FullName, Workspace.MetadataFolderName)))
                {
                    continue;
                }

                // Pushed in reverse, so they're popped in order.
                foreach (var subfolder in current.EnumerateDirectories("*", SubfolderOptions)
                             .OrderByDescending(subfolder => subfolder.Name, StringComparer.Ordinal))
                {
                    pending.Push((subfolder, depth + 1));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The folder went away or can't be read during the scan. Its repositories can't be listed.
            }
        }

        repositories.Sort(FileSystemPaths.Comparer);
        return repositories;
    }

    private static bool IsLinkedWorktree(string gitFile)
    {
        try
        {
            // The .git file of a worktree or submodule is a single line: "gitdir: <path>".
            string? line = File.ReadLines(gitFile).FirstOrDefault();
            const string prefix = "gitdir:";
            if (line is null || !line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string gitDir = line[prefix.Length..].Trim();
            if (gitDir.Length == 0)
            {
                return false;
            }

            gitDir = Path.GetFullPath(gitDir, Path.GetDirectoryName(gitFile)!);
            return File.Exists(Path.Combine(gitDir, CommonDirFileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
