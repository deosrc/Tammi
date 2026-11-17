namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A Local Repository opened in its own primary tab, outside any workspace. The terminals and git graphs in the tab
/// are kept like a workspace's, so the repository stands in for a workspace of one: its parent folder is the Workspace
/// Folder, so its worktree path is the repository itself. Nothing is ever written to that folder.
/// </summary>
public static class StandaloneRepository
{
    /// <summary>
    /// The prefix of the ID of a standalone repository, which is never the ID of a workspace.
    /// </summary>
    public const string IdPrefix = "repository:";

    /// <summary>
    /// The ID of the repository's tab, in place of a workspace's ID.
    /// </summary>
    public static string IdFor(string repositoryPath) => IdPrefix + Normalize(repositoryPath);

    /// <summary>
    /// The repository without a trailing folder separator.
    /// </summary>
    public static string Normalize(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        return Path.TrimEndingDirectorySeparator(repositoryPath);
    }

    /// <summary>
    /// The repository as a <see cref="WorkspaceRepository"/>. It has no workspace branch or merge target, so both are
    /// <c>HEAD</c>.
    /// </summary>
    public static WorkspaceRepository RepositoryOf(string repositoryPath)
    {
        var path = Normalize(repositoryPath);
        return new WorkspaceRepository(Path.GetFileName(path), path, "HEAD", "HEAD");
    }

    /// <summary>
    /// The workspace that stands in for the repository.
    /// </summary>
    public static Workspace WorkspaceOf(string repositoryPath)
    {
        var repository = RepositoryOf(repositoryPath);
        return new Workspace
        {
            Id = IdFor(repository.LocalRepositoryPath),
            Name = repository.FolderName,
            Folder = Path.GetDirectoryName(repository.LocalRepositoryPath) ?? repository.LocalRepositoryPath,
            Repositories = [repository],
        };
    }
}
