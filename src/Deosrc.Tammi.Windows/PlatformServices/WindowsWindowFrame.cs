using System.Runtime.InteropServices;
using Microsoft.Maui.ApplicationModel;
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
    private Microsoft.UI.Xaml.Window? _window;
    private bool _isSlim;
    private bool _isMaximized;

    public WindowsWindowFrame(ISettingsStore settings)
    {
        _settings = settings;
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

    // MAUI extends its page into the title bar and draws a title there; with no title bar there's nothing to extend
    // into. Without the setting, the title bar comes back as MAUI has it.
    private void ApplyTitleBar()
    {
        if (_window is null)
        {
            return;
        }

        _window.ExtendsContentIntoTitleBar = !_isSlim;
        if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: !_isSlim);
        }
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
