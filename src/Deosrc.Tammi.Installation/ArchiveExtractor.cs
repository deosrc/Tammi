using System.Formats.Tar;
using System.IO.Compression;

namespace Deosrc.Tammi.Installation;

/// <summary>
/// Unpacks release archives: <c>.zip</c> or <c>.tar.gz</c>.
/// </summary>
public static class ArchiveExtractor
{
    /// <summary>
    /// Unpacks an archive into a folder, which is created if needed.
    /// </summary>
    /// <exception cref="NotSupportedException">The archive isn't a <c>.zip</c> or <c>.tar.gz</c>.</exception>
    public static void Extract(string archivePath, string destination)
    {
        Directory.CreateDirectory(destination);
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, destination, overwriteFiles: true);
        }
        else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            using var file = File.OpenRead(archivePath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, destination, overwriteFiles: true);
        }
        else
        {
            throw new NotSupportedException($"'{Path.GetFileName(archivePath)}' isn't a .zip or .tar.gz archive.");
        }
    }
}
