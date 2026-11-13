using System.Diagnostics;

namespace Deosrc.Tammi.Installation;

/// <summary>
/// The link that starts Tammi from the desktop's menu: a <c>.desktop</c> file on Linux and a Start menu shortcut on
/// Windows. Pointing it at another version's executable is how an update takes effect.
/// </summary>
public static class MenuLink
{
    /// <summary>
    /// Creates the link, or points an existing one at a different executable.
    /// </summary>
    /// <returns>Where the link is.</returns>
    public static string Point(ReleaseTarget target, string executablePath)
    {
        return target.IsWindows ? PointWindowsShortcut(executablePath) : PointDesktopEntry(executablePath);
    }

    /// <summary>
    /// Where the link is for the current OS.
    /// </summary>
    public static string CurrentPath(ReleaseTarget target) => target.IsWindows
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Tammi.lnk")
        : Path.Combine(InstallLocation.XdgDataHome(), "applications", DesktopEntry.FileName);

    private static string PointDesktopEntry(string executablePath)
    {
        var path = CurrentPath(new ReleaseTarget("linux-x64"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Write beside the link and rename, so a menu reading the file never sees half of it.
        var temp = path + ".tmp";
        File.WriteAllText(temp, DesktopEntry.Render(executablePath));
        File.Move(temp, path, overwrite: true);
        return path;
    }

    private static string PointWindowsShortcut(string executablePath)
    {
        var path = CurrentPath(new ReleaseTarget("win-x64"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Windows PowerShell ships with Windows, and creates shortcuts through the shell's own COM object. The
        // paths go in through the environment so that none of their characters need escaping.
        const string script =
            "$s = (New-Object -ComObject WScript.Shell).CreateShortcut($env:TAMMI_LINK); " +
            "$s.TargetPath = $env:TAMMI_TARGET; " +
            "$s.WorkingDirectory = Split-Path $env:TAMMI_TARGET; " +
            "$s.Description = 'Tammi'; " +
            "$s.Save()";
        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-Command");
        info.ArgumentList.Add(script);
        info.Environment["TAMMI_LINK"] = path;
        info.Environment["TAMMI_TARGET"] = executablePath;

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("PowerShell couldn't be started to create the Start menu shortcut.");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Creating the Start menu shortcut failed: {error.Trim()}");
        }

        return path;
    }
}
