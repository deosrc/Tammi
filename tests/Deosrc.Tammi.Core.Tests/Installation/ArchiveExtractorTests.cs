using System.Formats.Tar;
using System.IO.Compression;
using Deosrc.Tammi.Core.Tests.Tools;
using Deosrc.Tammi.Installation;

namespace Deosrc.Tammi.Core.Tests.Installation;

public class ArchiveExtractorTests
{
    [Fact]
    public void ExtractsZip()
    {
        using var folder = new TempFolder();
        var source = Path.Combine(folder.Path, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "zip");
        var archive = Path.Combine(folder.Path, "x.zip");
        ZipFile.CreateFromDirectory(source, archive);

        ArchiveExtractor.Extract(archive, Path.Combine(folder.Path, "out"));

        Assert.Equal("zip", File.ReadAllText(Path.Combine(folder.Path, "out", "a.txt")));
    }

    [Fact]
    public void ExtractsTarGz()
    {
        using var folder = new TempFolder();
        var source = Path.Combine(folder.Path, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "tar");
        var archive = Path.Combine(folder.Path, "x.tar.gz");
        using (var file = File.Create(archive))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        {
            TarFile.CreateFromDirectory(source, gzip, includeBaseDirectory: false);
        }

        ArchiveExtractor.Extract(archive, Path.Combine(folder.Path, "out"));

        Assert.Equal("tar", File.ReadAllText(Path.Combine(folder.Path, "out", "a.txt")));
    }

    [Fact]
    public void RejectsOtherFormats() =>
        Assert.Throws<NotSupportedException>(() => ArchiveExtractor.Extract("x.rar", Path.GetTempPath()));
}
