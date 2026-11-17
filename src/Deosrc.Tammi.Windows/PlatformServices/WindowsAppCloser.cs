using Microsoft.Maui.ApplicationModel;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Windows.PlatformServices;

/// <summary>
/// Closes the main window, which runs <see cref="CloseConfirmation"/>.
/// </summary>
internal sealed class WindowsAppCloser : IAppCloser
{
    public void Close() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            is Microsoft.UI.Xaml.Window window)
        {
            window.Close();
        }
    });
}
