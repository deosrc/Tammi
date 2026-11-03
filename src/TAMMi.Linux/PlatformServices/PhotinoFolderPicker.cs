using Photino.NET;
using TAMMi.Core.Platform;

namespace TAMMi.Linux.PlatformServices;

/// <summary>
/// Shows the native folder dialog (GTK on Linux) through Photino.
/// </summary>
internal sealed class PhotinoFolderPicker(PhotinoWindow window) : IFolderPicker
{
    public async Task<string?> PickFolderAsync(
        FolderPickerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var folders = await window
            .ShowOpenFolderAsync(options?.Title ?? "Choose folder", options?.InitialDirectory, multiSelect: false)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        return folders?.FirstOrDefault(folder => !string.IsNullOrEmpty(folder));
    }
}
