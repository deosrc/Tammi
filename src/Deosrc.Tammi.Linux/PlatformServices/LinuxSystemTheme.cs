using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Linux.PlatformServices;

/// <summary>
/// Detects the desktop's dark mode from the XDG desktop portal, using <c>gdbus</c>, and watches for it changing.
/// </summary>
/// <remarks>
/// WebKitGTK doesn't always follow the desktop's preference, so the UI is told the theme instead. If <c>gdbus</c> or
/// the portal isn't available, the theme is undetected and the web view's own detection is used.
/// </remarks>
internal sealed class LinuxSystemTheme : ISystemTheme, IDisposable
{
    private const string PortalName = "org.freedesktop.portal.Desktop";
    private const string PortalPath = "/org/freedesktop/portal/desktop";

    private readonly ILogger<LinuxSystemTheme> _logger;
    private readonly Lock _lock = new();
    private bool? _isDark;
    private bool _started;
    private Process? _monitor;

    public LinuxSystemTheme(ILogger<LinuxSystemTheme> logger)
    {
        _logger = logger;
    }

    public bool? IsDark
    {
        get
        {
            lock (_lock)
            {
                if (!_started)
                {
                    _started = true;
                    _isDark = Read();
                    StartMonitor();
                }

                return _isDark;
            }
        }
    }

    public event Action? Changed;

    public void Dispose()
    {
        lock (_lock)
        {
            try
            {
                if (_monitor is { HasExited: false })
                {
                    _monitor.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            _monitor?.Dispose();
            _monitor = null;
        }
    }

    private bool? Read()
    {
        try
        {
            var start = new ProcessStartInfo("gdbus")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in new[]
            {
                "call", "--session", "--dest", PortalName, "--object-path", PortalPath,
                "--method", "org.freedesktop.portal.Settings.Read", "org.freedesktop.appearance", "color-scheme",
            })
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(TimeSpan.FromSeconds(5)))
            {
                process.Kill();
                return null;
            }

            return process.ExitCode == 0 ? PortalColorScheme.ParseIsDark(output) : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _logger.LogDebug(ex, "Couldn't read the system theme from the desktop portal.");
            return null;
        }
    }

    // Watches the portal's signals. A change to any setting is cheap to check, so only the colour scheme is looked for.
    private void StartMonitor()
    {
        try
        {
            var start = new ProcessStartInfo("gdbus")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in new[] { "monitor", "--session", "--dest", PortalName, "--object-path", PortalPath })
            {
                start.ArgumentList.Add(argument);
            }

            var monitor = Process.Start(start);
            if (monitor is null)
            {
                return;
            }

            _monitor = monitor;
            monitor.OutputDataReceived += (_, e) =>
            {
                if (e.Data is { } line && line.Contains("color-scheme", StringComparison.Ordinal))
                {
                    OnSchemeSignal();
                }
            };
            monitor.BeginOutputReadLine();
            monitor.BeginErrorReadLine();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogDebug(ex, "Couldn't watch the system theme, so changes to it won't be noticed.");
        }
    }

    private void OnSchemeSignal()
    {
        var isDark = Read();
        lock (_lock)
        {
            if (_isDark == isDark)
            {
                return;
            }

            _isDark = isDark;
        }

        Changed?.Invoke();
    }
}
