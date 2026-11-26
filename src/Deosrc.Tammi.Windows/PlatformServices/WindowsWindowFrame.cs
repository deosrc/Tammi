using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Platform;
using Microsoft.UI.Windowing;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Windows.PlatformServices;

/// <summary>
/// The frame of the main window. With the Slim Window setting on, the window has its title bar removed but keeps its
/// border, so Windows still resizes it, snaps it and draws its shadow. The app bar takes the title bar's place.
/// </summary>
internal sealed class WindowsWindowFrame : IWindowFrame
{
    private const uint WmNcLButtonDown = 0x00A1;
    private const nint HtCaption = 2;

    private readonly ISettingsStore _settings;
    private readonly ILogger<WindowsWindowFrame> _logger;
    private Microsoft.UI.Xaml.Window? _window;
    private bool _isSlim;
    private bool _isMaximized;

    public WindowsWindowFrame(ISettingsStore settings, ILogger<WindowsWindowFrame> logger)
    {
        _settings = settings;
        _logger = logger;
        _isSlim = settings.Current.General.SlimWindow;
        settings.Changed += OnSettingsChanged;
    }

    public bool HasTitleBar => !_isSlim;

    // The window keeps its border, which Windows resizes it by.
    public bool NeedsResizeEdges => false;

    public bool IsMaximized => _isMaximized;

    public event Action? Changed;

    /// <summary>
    /// Removes the window's title bar if the Slim Window setting is on, and follows the window's state. Call it when
    /// the window is created.
    /// </summary>
    public void Attach(Microsoft.UI.Xaml.Window window)
    {
        _window = window;

        // Otherwise the window is left as MAUI made it.
        if (_isSlim)
        {
            ApplyTitleBar();
        }

        _isMaximized = Presenter?.State == OverlappedPresenterState.Maximized;
        window.AppWindow.Changed += (_, args) =>
        {
            if (args.DidPresenterChange || args.DidSizeChange)
            {
                var maximized = Presenter?.State == OverlappedPresenterState.Maximized;
                if (maximized != _isMaximized)
                {
                    _isMaximized = maximized;
                    Changed?.Invoke();
                }
            }
        };
    }

    // Removes or restores the title bar when the setting is changed.
    private void OnSettingsChanged()
    {
        var slim = _settings.Current.General.SlimWindow;
        if (slim == _isSlim)
        {
            return;
        }

        _isSlim = slim;
        MainThread.BeginInvokeOnMainThread(ApplyTitleBar);
        Changed?.Invoke();
    }

    // Windows' title bar goes with the presenter's. MAUI also keeps space at the top of the page for a title bar of
    // its own, drawn under Windows' one, and doesn't see the presenter's change: the space stays, empty but for the
    // window's title. Without the setting, both come back as MAUI has them.
    private void ApplyTitleBar()
    {
        if (_window is null)
        {
            return;
        }

        if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: !_isSlim);
        }

        SetMauiTitleBarVisibility(!_isSlim);
    }

    // MAUI hides its title bar only when the window's style loses its caption, which the presenter leaves in place.
    // The method that hides it isn't public, so it's found by name. If a MAUI update renames it, the space comes back
    // but nothing else breaks.
    private void SetMauiTitleBarVisibility(bool isVisible)
    {
        var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows
            .FirstOrDefault(window => ReferenceEquals(window.Handler?.PlatformView, _window));
        var manager = mauiWindow?.Handler?.MauiContext?.Services.GetService<NavigationRootManager>();
        var setVisibility = typeof(NavigationRootManager).GetMethod(
            "SetTitleBarVisibility",
            BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(bool)]);
        if (manager is null || setVisibility is null)
        {
            _logger.LogWarning("Couldn't show or hide MAUI's title bar: its NavigationRootManager.SetTitleBarVisibility wasn't found");
            return;
        }

        setVisibility.Invoke(manager, [isVisible]);
    }

    public void Minimize() => MainThread.BeginInvokeOnMainThread(() => Presenter?.Minimize());

    public void ToggleMaximized() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (Presenter is not { } presenter)
        {
            return;
        }

        if (presenter.State == OverlappedPresenterState.Maximized)
        {
            presenter.Restore();
        }
        else
        {
            presenter.Maximize();
        }
    });

    // Hands the drag to Windows as if the title bar had been pressed, so it moves the window, snaps it, and restores a
    // maximised window as it's dragged away. Windows takes the pointer's position from the cursor.
    public void BeginMove(int screenX, int screenY) => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (_window is null)
        {
            return;
        }

        var handle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        GetCursorPos(out var cursor);
        ReleaseCapture();
        SendMessage(handle, WmNcLButtonDown, HtCaption, (nint)((cursor.Y << 16) | (cursor.X & 0xFFFF)));
    });

    // The window's own border resizes it.
    public void BeginResize(WindowEdge edge, int screenX, int screenY)
    {
    }

    private OverlappedPresenter? Presenter => _window?.AppWindow.Presenter as OverlappedPresenter;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
}
