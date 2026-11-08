using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// Finds the icon each tool is shown with (see <see cref="ToolIconSettings"/>). Application and custom icons are kept
/// once read, by path, until <see cref="Clear"/>.
/// </summary>
public sealed class ToolImages
{
    private readonly IApplicationIconProvider _applicationIcons;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Task<string?>> _applicationCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<string?>> _customCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates the service.
    /// </summary>
    public ToolImages(IApplicationIconProvider applicationIcons)
    {
        _applicationIcons = applicationIcons ?? throw new ArgumentNullException(nameof(applicationIcons));
    }

    /// <summary>
    /// Finds a tool's icon.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="executablePath">
    /// The full path of its executable, or <c>null</c> if it wasn't found (it then has no application icon).
    /// </param>
    /// <returns>The icon, or its generic icon when the application's or custom icon can't be found.</returns>
    public async Task<ToolImage> GetAsync(ToolDefinition tool, string? executablePath)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var icon = tool.Icon;
        var dataUri = icon.Source switch
        {
            ToolIconSource.Application when executablePath is not null =>
                await Cached(_applicationCache, executablePath, ReadApplicationIconAsync).ConfigureAwait(false),
            ToolIconSource.Custom when icon.CustomPath is { } path =>
                await Cached(_customCache, path, path => ImageDataUri.ReadAsync(path)).ConfigureAwait(false),
            _ => null,
        };
        return new ToolImage(dataUri, icon.Generic);
    }

    /// <summary>
    /// Forgets the icons read, so they're read again, e.g. after an application or a custom icon has changed.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _applicationCache.Clear();
            _customCache.Clear();
        }
    }

    private async Task<string?> ReadApplicationIconAsync(string path)
    {
        try
        {
            return await _applicationIcons.GetIconAsync(path).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            // Without its icon, the tool is shown with its generic one.
            return null;
        }
    }

    private Task<string?> Cached(Dictionary<string, Task<string?>> cache, string key, Func<string, Task<string?>> read)
    {
        lock (_lock)
        {
            if (!cache.TryGetValue(key, out var task))
            {
                task = Task.Run(() => read(key));
                cache[key] = task;
            }

            return task;
        }
    }
}
