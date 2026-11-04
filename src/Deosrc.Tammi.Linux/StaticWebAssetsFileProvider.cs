using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;

namespace Deosrc.Tammi.Linux;

/// <summary>
/// Serves static web assets (e.g. <c>_content/Deosrc.Tammi.UI/...</c> and <c>_framework/...</c>) from their
/// source locations, using the <c>*.staticwebassets.runtime.json</c> manifest written by the build.
/// </summary>
/// <remarks>
/// Photino.Blazor only serves files from the physical <c>wwwroot</c> folder in the output directory.
/// After <c>dotnet publish</c> every static web asset is copied there, but after <c>dotnet build</c>
/// assets from referenced Razor class libraries are not. ASP.NET Core and .NET MAUI resolve them through
/// this manifest, and this provider does the same for Photino.
/// </remarks>
internal sealed class StaticWebAssetsFileProvider : IFileProvider
{
    private readonly string[] _contentRoots;
    private readonly ManifestNode _root;

    private StaticWebAssetsFileProvider(string[] contentRoots, ManifestNode root)
    {
        _contentRoots = contentRoots;
        _root = root;
    }

    /// <summary>
    /// Creates a provider from the manifest next to the application, or returns <c>null</c> when there is
    /// no manifest (e.g. in a published app).
    /// </summary>
    public static StaticWebAssetsFileProvider? TryCreate()
    {
        var assemblyName = typeof(StaticWebAssetsFileProvider).Assembly.GetName().Name;
        var manifestPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.staticwebassets.runtime.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        using var stream = File.OpenRead(manifestPath);
        var manifest = JsonSerializer.Deserialize<Manifest>(stream);
        if (manifest?.ContentRoots is null || manifest.Root is null)
        {
            return null;
        }

        return new StaticWebAssetsFileProvider(manifest.ContentRoots, manifest.Root);
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var segments = subpath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var node = _root;
        for (var i = 0; i < segments.Length; i++)
        {
            if (node.Children is not null && node.Children.TryGetValue(segments[i], out var child))
            {
                node = child;
                continue;
            }

            // No explicit entry: fall back to a pattern on the deepest matching folder, if any.
            return FromPatterns(node, segments.Skip(i));
        }

        if (node.Asset is { } asset)
        {
            return CreateFileInfo(asset.ContentRootIndex, asset.SubPath);
        }

        return new NotFoundFileInfo(subpath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    private IFileInfo FromPatterns(ManifestNode node, IEnumerable<string> remainingSegments)
    {
        var relativePath = string.Join('/', remainingSegments);
        foreach (var pattern in node.Patterns ?? [])
        {
            var fileInfo = CreateFileInfo(pattern.ContentRootIndex, relativePath);
            if (fileInfo.Exists)
            {
                return fileInfo;
            }
        }

        return new NotFoundFileInfo(relativePath);
    }

    private IFileInfo CreateFileInfo(int contentRootIndex, string subPath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_contentRoots[contentRootIndex], subPath));
        var fileInfo = new FileInfo(fullPath);
        return fileInfo.Exists
            ? new PhysicalFileInfo(fileInfo)
            : new NotFoundFileInfo(subPath);
    }

    private sealed record Manifest(string[]? ContentRoots, ManifestNode? Root);

    private sealed record ManifestNode(
        Dictionary<string, ManifestNode>? Children,
        ManifestAsset? Asset,
        ManifestPattern[]? Patterns);

    private sealed record ManifestAsset(int ContentRootIndex, string SubPath);

    private sealed record ManifestPattern(int ContentRootIndex, string Pattern, int Depth);
}
