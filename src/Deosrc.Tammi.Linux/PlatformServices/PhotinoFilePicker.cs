using Photino.NET;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Linux.PlatformServices;

/// <summary>
/// Shows the native file dialog (GTK on Linux) through Photino.
/// </summary>
internal sealed class PhotinoFilePicker(PhotinoWindow window) : IFilePicker
{
    public async Task<string?> PickFileAsync(
        FilePickerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        (string Name, string[] Extensions)[]? filters = options is { Extensions.Count: > 0 }
            ? [(options.FileTypeName ?? "Files", options.Extensions.Select(extension => "*" + extension).ToArray())]
            : null;
        var files = await window
            .ShowOpenFileAsync(options?.Title ?? "Choose file", options?.InitialDirectory, multiSelect: false, filters)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        return files?.FirstOrDefault(file => !string.IsNullOrEmpty(file));
    }
}
