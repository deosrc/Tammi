namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// How a built-in tool opens a folder.
/// </summary>
public enum BuiltInToolKind
{
    /// <summary>
    /// Its executable is started with the folder's path.
    /// </summary>
    Folder,

    /// <summary>
    /// It opens solutions, not folders: there's an entry for each <c>.sln</c> and <c>.slnx</c> file found in the folder
    /// (see <see cref="SolutionFinder"/>).
    /// </summary>
    Solution,

    /// <summary>
    /// VS Code opens the folder in its dev container. There's only an entry when the folder has a dev container
    /// configuration (see <see cref="DevContainerConfig"/>).
    /// </summary>
    DevContainer,

    /// <summary>
    /// The folder is opened with the system's default application for folders, i.e. the file manager.
    /// </summary>
    FileManager,
}
