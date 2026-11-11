namespace Deosrc.Tammi.UI.Layout;

/// <summary>
/// Whether the AI usage page has a tab open. The usage footer opens it, and the tab's close button closes it.
/// </summary>
public sealed class UsageTabState
{
    public bool IsOpen { get; private set; }

    /// <summary>
    /// Raised when <see cref="IsOpen"/> changes. It can be raised on any thread.
    /// </summary>
    public event Action? Changed;

    public void Open()
    {
        if (!IsOpen)
        {
            IsOpen = true;
            Changed?.Invoke();
        }
    }

    public void Close()
    {
        if (IsOpen)
        {
            IsOpen = false;
            Changed?.Invoke();
        }
    }
}
