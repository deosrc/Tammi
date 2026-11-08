using Microsoft.Maui.ApplicationModel;
using Deosrc.Tammi.Core.Platform;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Deosrc.Tammi.Windows.PlatformServices;

/// <summary>
/// Shows the native Windows folder dialog.
/// </summary>
/// <remarks>
/// The Windows picker can't set a title or start in a given folder, so <see cref="FolderPickerOptions"/> is ignored.
/// </remarks>
internal sealed class WindowsFolderPicker : IFolderPicker
{
    public Task<string?> PickFolderAsync(
        FolderPickerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
            picker.FileTypeFilter.Add("*");

            // An unpackaged app must give the picker a parent window.
            InitializeWithWindow.Initialize(picker, GetMainWindowHandle());

            var folder = await picker.PickSingleFolderAsync().AsTask(cancellationToken);
            return folder?.Path;
        });
    }

    /// <summary>
    /// The handle of the app's main window, which the pickers are shown over.
    /// </summary>
    internal static nint GetMainWindowHandle()
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("There is no main window to show the picker over.");
        return WindowNative.GetWindowHandle(window);
    }
}
