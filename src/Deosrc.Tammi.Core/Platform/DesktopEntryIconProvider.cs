namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Finds applications' icons on Linux from their desktop entries (<c>.desktop</c> files) and the icon theme, as
/// desktop environments do.
/// </summary>
/// <remarks>
/// <para>
/// The desktop entry is looked for in the <c>applications</c> folder of each XDG data folder (<c>$XDG_DATA_HOME</c>,
/// then <c>$XDG_DATA_DIRS</c>), which includes Flatpak's and Snap's when they're installed. An entry belongs to the
/// executable when its <c>TryExec</c> or the command in its <c>Exec</c> is the same file, or has the same name, ignoring
/// a <c>.sh</c> extension (e.g. JetBrains Toolbox's <c>rider</c> script and Rider's <c>rider.sh</c>).
/// </para>
/// <para>
/// The entry's <c>Icon</c> is either a path, or a name looked for in the <c>hicolor</c> icon theme (scalable first, then
/// the largest size) and then in <c>/usr/share/pixmaps</c>.
/// </para>
/// </remarks>
public sealed class DesktopEntryIconProvider : IApplicationIconProvider
{
    private static readonly int[] IconSizes = [512, 256, 192, 128, 96, 64, 48, 32, 24, 16];

    private readonly Func<IReadOnlyList<string>> _getDataDirectories;

    /// <summary>
    /// Creates a provider that reads the XDG data folders from the environment.
    /// </summary>
    public DesktopEntryIconProvider()
        : this(DataDirectoriesFromEnvironment)
    {
    }

    /// <summary>
    /// Creates a provider that looks in <paramref name="dataDirectories"/>, in order.
    /// </summary>
    internal DesktopEntryIconProvider(IReadOnlyList<string> dataDirectories)
        : this(() => dataDirectories)
    {
    }

    private DesktopEntryIconProvider(Func<IReadOnlyList<string>> getDataDirectories)
    {
        _getDataDirectories = getDataDirectories;
    }

    /// <inheritdoc />
    public async Task<string?> GetIconAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var dataDirectories = _getDataDirectories();
        var icon = FindIconName(executablePath, dataDirectories);
        if (icon is null || FindIconFile(icon, dataDirectories) is not { } file)
        {
            return null;
        }

        return await ImageDataUri.ReadAsync(file, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> DataDirectoriesFromEnvironment()
    {
        var home = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } dataHome
            ? dataHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        var dirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS") is { Length: > 0 } dataDirs
            ? dataDirs
            : "/usr/local/share:/usr/share";
        return [home, .. dirs.Split(':', StringSplitOptions.RemoveEmptyEntries)];
    }

    // The Icon of the first desktop entry that belongs to the executable: one naming the same file wins over one with
    // the same name.
    private static string? FindIconName(string executablePath, IReadOnlyList<string> dataDirectories)
    {
        var target = RealPath(executablePath);
        var name = CommandName(executablePath);
        string? sameName = null;

        foreach (var file in DesktopFiles(dataDirectories))
        {
            var entry = ReadDesktopEntry(file);
            if (entry.Icon is null)
            {
                continue;
            }

            var commands = new[] { entry.TryExec, FirstWord(entry.Exec) }.OfType<string>().ToArray();
            if (commands.Any(command => Path.IsPathRooted(command) && RealPath(command) == target))
            {
                return entry.Icon;
            }

            if (sameName is null && commands.Any(command => CommandName(command) == name))
            {
                sameName = entry.Icon;
            }
        }

        return sameName;
    }

    private static IEnumerable<string> DesktopFiles(IReadOnlyList<string> dataDirectories)
    {
        foreach (var directory in dataDirectories)
        {
            var applications = Path.Combine(directory, "applications");
            IEnumerable<string> files;
            try
            {
                files = Directory.Exists(applications)
                    ? Directory.EnumerateFiles(applications, "*.desktop", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        MaxRecursionDepth = 2,
                        IgnoreInaccessible = true,
                    }).Order(StringComparer.Ordinal).ToArray()
                    : [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                files = [];
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    private static (string? Exec, string? TryExec, string? Icon) ReadDesktopEntry(string path)
    {
        string? exec = null, tryExec = null, icon = null;
        try
        {
            var inMainGroup = false;
            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (line.StartsWith('['))
                {
                    // Actions have their own groups, with their own Exec, so only the main group is read.
                    inMainGroup = line == "[Desktop Entry]";
                    continue;
                }

                var equals = line.IndexOf('=');
                if (!inMainGroup || equals <= 0)
                {
                    continue;
                }

                var key = line[..equals].Trim();
                var value = line[(equals + 1)..].Trim();
                switch (key)
                {
                    case "Exec":
                        exec = value;
                        break;
                    case "TryExec":
                        tryExec = value;
                        break;
                    case "Icon":
                        icon = value.Length > 0 ? value : null;
                        break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, null, null);
        }

        return (exec, tryExec, icon);
    }

    // The command in an Exec line: its first word, which may be in double quotes.
    private static string? FirstWord(string? exec)
    {
        if (string.IsNullOrWhiteSpace(exec))
        {
            return null;
        }

        exec = exec.TrimStart();
        if (exec.StartsWith('"'))
        {
            var end = exec.IndexOf('"', 1);
            return end > 1 ? exec[1..end] : null;
        }

        var space = exec.IndexOf(' ');
        return space < 0 ? exec : exec[..space];
    }

    private static string CommandName(string command)
    {
        var name = Path.GetFileName(command);
        return name.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) ? name[..^3] : name;
    }

    private static string RealPath(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? file.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Path.GetFullPath(path);
        }
    }

    private static string? FindIconFile(string icon, IReadOnlyList<string> dataDirectories)
    {
        if (Path.IsPathRooted(icon))
        {
            return File.Exists(icon) && ImageDataUri.IsImage(icon) ? icon : null;
        }

        var candidates = new List<string>();
        foreach (var directory in dataDirectories)
        {
            var theme = Path.Combine(directory, "icons", "hicolor");
            candidates.Add(Path.Combine(theme, "scalable", "apps", icon + ".svg"));
            candidates.AddRange(IconSizes.Select(size =>
                Path.Combine(theme, $"{size}x{size}", "apps", icon + ".png")));
        }

        foreach (var directory in dataDirectories)
        {
            candidates.Add(Path.Combine(directory, "pixmaps", icon + ".svg"));
            candidates.Add(Path.Combine(directory, "pixmaps", icon + ".png"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
