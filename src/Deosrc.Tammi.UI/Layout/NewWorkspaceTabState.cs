using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Workspaces;
using Deosrc.Tammi.UI.Workspaces;

namespace Deosrc.Tammi.UI.Layout;

/// <summary>
/// The Create Workspace flow's tab, and the flow's progress while it's open. Switching to another tab keeps the
/// progress, so the flow can be resumed. The tab's close button, cancelling the flow or creating the workspace closes
/// it and discards the progress.
/// </summary>
public sealed class NewWorkspaceTabState(ISettingsStore settings, WorkspaceCreator creator, WorkspaceTabs tabs)
{
    public bool IsOpen => Session is not null;

    public NewWorkspaceSession? Session { get; private set; }

    /// <summary>
    /// Raised when <see cref="IsOpen"/> changes. It can be raised on any thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Opens the tab if it isn't open, and returns its session.
    /// </summary>
    public NewWorkspaceSession Open()
    {
        if (Session is null)
        {
            var general = settings.Current.General;
            Session = new NewWorkspaceSession(
                new WorkspaceDraft(
                    general.DefaultWorkspaceParentFolder,
                    general.WorkspaceFolderNameFormat,
                    general.WorkspaceNameTemplate),
                creator,
                tabs);
            Session.Completed += _ => Close();
            Changed?.Invoke();
        }

        return Session;
    }

    public void Close()
    {
        if (Session is { } session)
        {
            Session = null;
            // Stops a creation that's running, and removes what was created.
            session.CancelCreating();
            Changed?.Invoke();
        }
    }
}
