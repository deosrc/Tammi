using System.Text;
using System.Text.Json;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// A folder's dev container configuration, which VS Code's Dev Containers extension opens the folder with (see
/// <see cref="BuiltInTool.DevContainer"/>).
/// </summary>
/// <param name="FilePath">The configuration file's full path.</param>
/// <param name="ContainerFolder">
/// Where the folder is in the container: the configuration's <c>workspaceFolder</c>, or
/// <c>/workspaces/&lt;folder name&gt;</c> by default.
/// </param>
public sealed record DevContainerConfig(string FilePath, string ContainerFolder)
{
    // The places the Dev Containers extension looks for a configuration when it opens a folder, in its order.
    // Configurations in subfolders of .devcontainer/ need the user to pick one, so they're not offered.
    private static readonly string[][] FileLocations =
    [
        [".devcontainer", "devcontainer.json"],
        [".devcontainer.json"],
    ];

    private const string FolderBasenameVariable = "${localWorkspaceFolderBasename}";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Finds a folder's dev container configuration.
    /// </summary>
    /// <returns>The configuration, or <c>null</c> if the folder doesn't have one.</returns>
    public static DevContainerConfig? Find(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        foreach (var location in FileLocations)
        {
            var path = Path.Combine([root, .. location]);
            if (File.Exists(path))
            {
                return new DevContainerConfig(path, ReadContainerFolder(path, Path.GetFileName(root)));
            }
        }

        return null;
    }

    /// <summary>
    /// The URI that opens a folder in its dev container with VS Code's <c>--folder-uri</c> option.
    /// </summary>
    /// <param name="folder">The folder's full path on this machine.</param>
    /// <param name="containerFolder">Where the folder is in the container, e.g. <c>/workspaces/api</c>.</param>
    /// <remarks>
    /// The URI's authority is <c>dev-container+</c> followed by the folder's path in hexadecimal UTF-8, and its path is
    /// the folder in the container.
    /// </remarks>
    public static string BuildFolderUri(string folder, string containerFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerFolder);

        var hostPath = Convert.ToHexStringLower(Encoding.UTF8.GetBytes(folder));
        var segments = containerFolder.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString);
        return $"vscode-remote://dev-container+{hostPath}/{string.Join('/', segments)}";
    }

    private static string ReadContainerFolder(string configPath, string folderName)
    {
        var defaultFolder = "/workspaces/" + folderName;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath), DocumentOptions);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("workspaceFolder", out var property)
                && property.ValueKind == JsonValueKind.String
                && property.GetString() is { Length: > 0 } workspaceFolder)
            {
                var expanded = workspaceFolder.Replace(FolderBasenameVariable, folderName, StringComparison.Ordinal);

                // Other variables (e.g. environment variables) can't be filled in here, so the default is used.
                return expanded.StartsWith('/') && !expanded.Contains("${", StringComparison.Ordinal)
                    ? expanded
                    : defaultFolder;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // The extension reports problems with the file when it opens the folder.
        }

        return defaultFolder;
    }
}
