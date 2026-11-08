namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Shows the platform's native dialog for choosing a file.
/// </summary>
public interface IFilePicker
{
    /// <summary>
    /// Asks the user to choose a file.
    /// </summary>
    /// <returns>The full path of the chosen file, or <c>null</c> if the user cancelled.</returns>
    Task<string?> PickFileAsync(FilePickerOptions? options = null, CancellationToken cancellationToken = default);
}
