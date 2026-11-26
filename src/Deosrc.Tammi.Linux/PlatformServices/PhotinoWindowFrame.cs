using System.Runtime.InteropServices;
using Photino.NET;
using Deosrc.Tammi.Core;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Linux.PlatformServices;

/// <summary>
/// The frame of the Photino main window. With the Slim Window setting on, the window is undecorated: it has no title bar
/// and no resize borders, so the page offers both. Moving and resizing are handed to the window manager through GTK,
/// which works on Wayland as well as X11.
/// </summary>
/// <remarks>
/// Photino doesn't give out its GTK window, so it's found among the app's top-level windows by its title.
/// </remarks>
internal sealed partial class PhotinoWindowFrame : IWindowFrame
{
    private const string Gtk = "libgtk-3.so.0";
    private const string GLib = "libglib-2.0.so.0";

    // The primary mouse button, as GTK numbers it.
    private const int PrimaryButton = 1;

    private readonly PhotinoWindow _window;
    private readonly ISettingsStore _settings;
    private bool _isSlim;
    private bool _isMaximized;

    /// <summary>
    /// Follows the window, which is already undecorated if the Slim Window setting is on (see
    /// <see cref="IsSlim"/>).
    /// </summary>
    public PhotinoWindowFrame(PhotinoWindow window, ISettingsStore settings)
    {
        _window = window;
        _settings = settings;
        _isSlim = IsSlim(settings);
        _isMaximized = window.Maximized;
        settings.Changed += OnSettingsChanged;

        // The window manager can maximise and restore the window too (e.g. from a keyboard shortcut).
        window
            .RegisterMaximizedHandler((_, _) => Update())
            .RegisterRestoredHandler((_, _) => Update())
            .RegisterSizeChangedHandler((_, _) => Update());
    }

    public bool HasTitleBar => !_isSlim;

    public bool NeedsResizeEdges => true;

    public bool IsMaximized => _isMaximized;

    public event Action? Changed;

    public void Minimize() => _window.Invoke(() => _window.SetMinimized(true));

    public void ToggleMaximized() => _window.Invoke(() =>
    {
        _window.SetMaximized(!_window.Maximized);
        Update();
    });

    public void BeginMove(int screenX, int screenY) => _window.Invoke(() =>
    {
        if (FindGtkWindow() is var gtkWindow && gtkWindow != IntPtr.Zero)
        {
            gtk_window_begin_move_drag(gtkWindow, PrimaryButton, screenX, screenY, 0);
        }
    });

    public void BeginResize(WindowEdge edge, int screenX, int screenY) => _window.Invoke(() =>
    {
        if (FindGtkWindow() is var gtkWindow && gtkWindow != IntPtr.Zero)
        {
            gtk_window_begin_resize_drag(gtkWindow, (int)edge, PrimaryButton, screenX, screenY, 0);
        }
    });

    /// <summary>
    /// Whether the window is to have no title bar: whether the Slim Window setting is on.
    /// </summary>
    public static bool IsSlim(ISettingsStore settings) => settings.Current.General.SlimWindow;

    // Adds or removes the window's decorations when the setting is changed.
    private void OnSettingsChanged()
    {
        var slim = IsSlim(_settings);
        if (slim == _isSlim)
        {
            return;
        }

        _isSlim = slim;
        _window.Invoke(() =>
        {
            if (FindGtkWindow() is var gtkWindow && gtkWindow != IntPtr.Zero)
            {
                gtk_window_set_decorated(gtkWindow, !slim);
            }
        });
        Changed?.Invoke();
    }

    private void Update()
    {
        var maximized = _window.Maximized;
        if (maximized != _isMaximized)
        {
            _isMaximized = maximized;
            Changed?.Invoke();
        }
    }

    // The main window: the visible top-level window with the app's title. Message boxes and file pickers have other
    // titles. Call it on the GTK thread.
    private static IntPtr FindGtkWindow()
    {
        var list = gtk_window_list_toplevels();
        try
        {
            for (var node = list; node != IntPtr.Zero; node = Marshal.ReadIntPtr(node, IntPtr.Size))
            {
                var window = Marshal.ReadIntPtr(node);
                if (gtk_widget_get_visible(window)
                    && Marshal.PtrToStringUTF8(gtk_window_get_title(window)) == AppInfo.Name)
                {
                    return window;
                }
            }

            return IntPtr.Zero;
        }
        finally
        {
            g_list_free(list);
        }
    }

    // A new GList of the windows, which aren't referenced, so only the list is freed. Each node is
    // { data, next, prev }.
    [LibraryImport(Gtk)]
    private static partial IntPtr gtk_window_list_toplevels();

    [LibraryImport(GLib)]
    private static partial void g_list_free(IntPtr list);

    [LibraryImport(Gtk)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool gtk_widget_get_visible(IntPtr widget);

    // Owned by the window, so not freed.
    [LibraryImport(Gtk)]
    private static partial IntPtr gtk_window_get_title(IntPtr window);

    [LibraryImport(Gtk)]
    private static partial void gtk_window_set_decorated(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool decorated);

    [LibraryImport(Gtk)]
    private static partial void gtk_window_begin_move_drag(IntPtr window, int button, int rootX, int rootY, uint timestamp);

    [LibraryImport(Gtk)]
    private static partial void gtk_window_begin_resize_drag(
        IntPtr window, int edge, int button, int rootX, int rootY, uint timestamp);
}
