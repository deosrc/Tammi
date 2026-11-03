namespace TAMMi.Core.Platform;

/// <summary>
/// The folders where TAMMi keeps its own files outside any workspace (see <c>docs/CONTEXT.md</c>).
/// </summary>
/// <remarks>
/// The folders might not exist yet. Create them before writing to them.
/// </remarks>
public interface IAppPaths
{
    /// <summary>
    /// The folder for app settings.
    /// </summary>
    string ConfigDirectory { get; }

    /// <summary>
    /// The folder for app data that must be kept, such as the index of known workspaces.
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// The folder for data that can be rebuilt and is safe to delete.
    /// </summary>
    string CacheDirectory { get; }
}
