namespace Deosrc.Tammi.UI.Visualiser;

/// <summary>
/// The visualiser's own window, which each host provides.
/// </summary>
public interface IVisualiserWindow
{
    /// <summary>
    /// Opens the visualiser's window, or brings it to the front if it's already open.
    /// </summary>
    void OpenOrFocus();
}
