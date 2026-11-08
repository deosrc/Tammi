namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Reads image files as <c>data:</c> URIs, so the UI can show them without serving the files.
/// </summary>
public static class ImageDataUri
{
    /// <summary>
    /// The largest image read. Icons are much smaller, and a larger file is probably not an icon.
    /// </summary>
    public const long MaxFileSize = 1024 * 1024;

    private static readonly Dictionary<string, string> MediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".svg"] = "image/svg+xml",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".ico"] = "image/x-icon",
        [".bmp"] = "image/bmp",
    };

    /// <summary>
    /// The image file extensions that can be read, e.g. <c>.png</c>.
    /// </summary>
    public static IReadOnlyCollection<string> Extensions => MediaTypes.Keys;

    /// <summary>
    /// Whether a file's extension is one of the image types that can be read.
    /// </summary>
    public static bool IsImage(string path) => MediaTypes.ContainsKey(Path.GetExtension(path));

    /// <summary>
    /// Reads an image file.
    /// </summary>
    /// <returns>
    /// The image as a <c>data:</c> URI, or <c>null</c> if it isn't a known image type, is empty or larger than
    /// <see cref="MaxFileSize"/>, or can't be read.
    /// </returns>
    public static async Task<string?> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!MediaTypes.TryGetValue(Path.GetExtension(path), out var mediaType))
        {
            return null;
        }

        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length == 0 || file.Length > MaxFileSize)
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return FromBytes(mediaType, bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes image data as a <c>data:</c> URI.
    /// </summary>
    public static string FromBytes(string mediaType, byte[] bytes) =>
        $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
}
