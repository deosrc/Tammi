using Deosrc.Tammi.Core.Platform;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Deosrc.Tammi.Windows.PlatformServices;

/// <summary>
/// Finds applications' icons on Windows: the icon the shell shows for the executable, as a PNG.
/// </summary>
/// <remarks>
/// A batch file (e.g. VS Code's <c>bin\code.cmd</c>) has the shell's generic icon, so the icon of an executable with
/// the same name in the folder above it (e.g. <c>Code.exe</c>) is used instead, if there is one.
/// </remarks>
internal sealed class WindowsApplicationIconProvider : IApplicationIconProvider
{
    private const uint IconSize = 64;

    public async Task<string?> GetIconAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var path = Path.GetFullPath(executablePath);
        if (Path.GetExtension(path).ToUpperInvariant() is ".CMD" or ".BAT")
        {
            var application = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(path)!, "..", Path.GetFileNameWithoutExtension(path) + ".exe"));
            if (!File.Exists(application))
            {
                return null;
            }

            path = application;
        }

        var file = await StorageFile.GetFileFromPathAsync(path).AsTask(cancellationToken);
        using var thumbnail = await file
            .GetThumbnailAsync(ThumbnailMode.SingleItem, IconSize, ThumbnailOptions.UseCurrentScale)
            .AsTask(cancellationToken);
        if (thumbnail is null || thumbnail.Size == 0)
        {
            return null;
        }

        // The thumbnail is a bitmap in whatever format the shell chose, so it's encoded again as a PNG.
        var decoder = await BitmapDecoder.CreateAsync(thumbnail).AsTask(cancellationToken);
        using var bitmap = await decoder
            .GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
            .AsTask(cancellationToken);
        using var png = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, png).AsTask(cancellationToken);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync().AsTask(cancellationToken);

        var bytes = new byte[png.Size];
        using var reader = new DataReader(png.GetInputStreamAt(0));
        await reader.LoadAsync((uint)png.Size).AsTask(cancellationToken);
        reader.ReadBytes(bytes);
        return ImageDataUri.FromBytes("image/png", bytes);
    }
}
