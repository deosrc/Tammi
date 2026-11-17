using Photino.NET;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Linux.PlatformServices;

/// <summary>
/// Closes the Photino main window, which runs the window's closing handler.
/// </summary>
internal sealed class PhotinoAppCloser(PhotinoWindow window) : IAppCloser
{
    public void Close() => window.Invoke(window.Close);
}
