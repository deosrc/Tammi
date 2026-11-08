using Microsoft.Maui.ApplicationModel;
using Deosrc.Tammi.Core.Platform;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Deosrc.Tammi.Windows.PlatformServices;

/// <summary>
/// Shows the native Windows file dialog.
/// </summary>
/// <remarks>
/// The Windows picker can't set a title or start in a given folder, so those options are ignored.
/// </remarks>
internal sealed class WindowsFilePicker : IFilePicker
{
    public Task<string?> PickFileAsync(
        FilePickerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
            if (options is { Extensions.Count: > 0 })
            {
                foreach (var extension in options.Extensions)
                {
                    picker.FileTypeFilter.Add(extension);
                }
            }
            else
            {
                picker.FileTypeFilter.Add("*");
            }

            // An unpackaged app must give the picker a parent window.
            InitializeWithWindow.Initialize(picker, WindowsFolderPicker.GetMainWindowHandle());

            var file = await picker.PickSingleFileAsync().AsTask(cancellationToken);
            return file?.Path;
        });
    }
}
