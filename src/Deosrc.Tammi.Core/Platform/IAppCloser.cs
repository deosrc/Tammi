namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Closes the app's main window, the same as if the user had closed it: the usual confirmation is asked first, e.g.
/// when agents are running.
/// </summary>
public interface IAppCloser
{
    /// <summary>
    /// Asks the main window to close. The user may cancel it.
    /// </summary>
    void Close();
}
