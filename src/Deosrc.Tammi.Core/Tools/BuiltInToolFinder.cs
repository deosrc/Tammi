using System.ComponentModel;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// Finds which built-in tools (see <see cref="BuiltInTool"/>) are installed, and the executable that starts
/// each one.
/// </summary>
/// <remarks>
/// Each tool is looked for on the search path, then where its installer usually puts it. The file manager is always
/// there.
/// </remarks>
public sealed class BuiltInToolFinder
{
    private static readonly TimeSpan VsWhereTimeout = TimeSpan.FromSeconds(15);

    private readonly IProcessLauncher _processLauncher;
    private readonly Func<string, string?> _getEnvironmentVariable;

    /// <summary>
    /// Creates a finder that finds executables with <paramref name="processLauncher"/>.
    /// </summary>
    public BuiltInToolFinder(IProcessLauncher processLauncher)
        : this(processLauncher, Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>
    /// Creates a finder that reads environment variables with <paramref name="getEnvironmentVariable"/>.
    /// </summary>
    internal BuiltInToolFinder(IProcessLauncher processLauncher, Func<string, string?> getEnvironmentVariable)
    {
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
    }

    /// <summary>
    /// Finds the installed built-in tools.
    /// </summary>
    /// <returns>The installed tools, in the order of <see cref="BuiltInTool.All"/>.</returns>
    public async Task<IReadOnlyList<InstalledTool>> FindAsync(CancellationToken cancellationToken = default)
    {
        var vsCode = FindVsCode();
        var installed = new List<InstalledTool>();
        foreach (var tool in BuiltInTool.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = await FindAsync(tool, vsCode, cancellationToken).ConfigureAwait(false);
            if (found is not null)
            {
                installed.Add(found);
            }
        }

        return installed;
    }

    private async Task<InstalledTool?> FindAsync(BuiltInTool tool, string? vsCode, CancellationToken cancellationToken)
    {
        if (tool == BuiltInTool.FileManager)
        {
            return new InstalledTool(tool, null);
        }

        if (tool == BuiltInTool.VsCode)
        {
            return Installed(tool, vsCode);
        }

        if (tool == BuiltInTool.DevContainer)
        {
            return HasDevContainersExtension() ? Installed(tool, vsCode) : null;
        }

        if (tool == BuiltInTool.Rider)
        {
            return Installed(tool, FindRider());
        }

        if (tool == BuiltInTool.VisualStudio)
        {
            return Installed(tool, await FindVisualStudioAsync(cancellationToken).ConfigureAwait(false));
        }

        return null;
    }

    private static InstalledTool? Installed(BuiltInTool tool, string? executablePath) =>
        executablePath is null ? null : new InstalledTool(tool, executablePath);

    private string? FindVsCode()
    {
        if (!OperatingSystem.IsWindows())
        {
            return FindFirst("code", "/snap/bin/code", "/usr/share/code/bin/code");
        }

        // On the search path, VS Code is code.cmd in its bin folder. Code.exe, beside that folder, opens folders too,
        // without a console window.
        if (_processLauncher.FindExecutable("code") is { } command)
        {
            var exe = Path.Combine(Path.GetDirectoryName(command)!, "..", "Code.exe");
            return File.Exists(exe) ? Path.GetFullPath(exe) : command;
        }

        return FindFirst(
            InFolder("LOCALAPPDATA", "Programs", "Microsoft VS Code", "Code.exe"),
            InFolder("ProgramFiles", "Microsoft VS Code", "Code.exe"));
    }

    private bool HasDevContainersExtension()
    {
        var extensions = _getEnvironmentVariable("VSCODE_EXTENSIONS") is { Length: > 0 } folder
            ? folder
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vscode", "extensions");
        try
        {
            return Directory.Exists(extensions)
                && Directory.EnumerateDirectories(extensions, "ms-vscode-remote.remote-containers-*").Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string? FindRider()
    {
        if (!OperatingSystem.IsWindows())
        {
            return FindFirst(
                "rider",
                InFolder("HOME", ".local", "share", "JetBrains", "Toolbox", "scripts", "rider"),
                "/snap/bin/rider");
        }

        return FindFirst(
                "rider",
                InFolder("LOCALAPPDATA", "JetBrains", "Toolbox", "scripts", "rider.cmd"),
                "rider64",
                InFolder("LOCALAPPDATA", "Programs", "Rider", "bin", "rider64.exe"))
            ?? FindLatest(InFolder("ProgramFiles", "JetBrains"), "JetBrains Rider*", "bin", "rider64.exe");
    }

    private async Task<string?> FindVisualStudioAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        // vswhere comes with the Visual Studio Installer, and finds every installed version.
        var vsWhere = InFolder("ProgramFiles(x86)", "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (vsWhere is not null && File.Exists(vsWhere))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(VsWhereTimeout);
            try
            {
                var result = await _processLauncher.RunAsync(
                        new ProcessRequest(vsWhere, "-latest", "-prerelease", "-property", "productPath", "-utf8"),
                        timeout.Token)
                    .ConfigureAwait(false);
                var path = result.StandardOutput.Split('\n').Select(line => line.Trim())
                    .FirstOrDefault(line => line.Length > 0);
                if (result.Succeeded && path is not null && File.Exists(path))
                {
                    return path;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // vswhere didn't answer in time, so look on the search path instead.
            }
            catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException
                or UnauthorizedAccessException)
            {
                // vswhere couldn't be run, so look on the search path instead.
            }
        }

        return FindFirst("devenv");
    }

    // The first of the candidates that's found: names are looked for on the search path, and paths are checked.
    private string? FindFirst(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate) && _processLauncher.FindExecutable(candidate) is { } path)
            {
                return path;
            }
        }

        return null;
    }

    // The executable in the last (so usually the newest) of the folders matching a pattern, e.g. one folder per
    // installed version.
    private string? FindLatest(string? parent, string pattern, params string[] executable)
    {
        if (parent is null || !Directory.Exists(parent))
        {
            return null;
        }

        try
        {
            return Directory.EnumerateDirectories(parent, pattern)
                .OrderDescending(StringComparer.OrdinalIgnoreCase)
                .Select(folder => FindFirst(Path.Combine([folder, .. executable])))
                .FirstOrDefault(path => path is not null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // A path in the folder an environment variable names, or null if it isn't set.
    private string? InFolder(string variable, params string[] parts) =>
        _getEnvironmentVariable(variable) is { Length: > 0 } folder ? Path.Combine([folder, .. parts]) : null;
}
