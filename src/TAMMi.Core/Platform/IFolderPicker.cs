namespace TAMMi.Core.Platform;

/// <summary>
/// Shows the platform's native dialog for choosing a folder.
/// </summary>
public interface IFolderPicker
{
    /// <summary>
    /// Asks the user to choose a folder.
    /// </summary>
    /// <returns>The full path of the chosen folder, or <c>null</c> if the user cancelled.</returns>
    Task<string?> PickFolderAsync(FolderPickerOptions? options = null, CancellationToken cancellationToken = default);
}
