using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// One run of the Create Workspace flow. It outlives the page, so switching to another tab and back resumes it, and a
/// creation that's running carries on meanwhile.
/// </summary>
public sealed class NewWorkspaceSession(WorkspaceDraft draft, WorkspaceCreator creator, WorkspaceTabs tabs)
{
    private CancellationTokenSource? _creation;

    public WorkspaceDraft Draft { get; } = draft;

    public NewWorkspaceStep Step { get; set; } = NewWorkspaceStep.Tasks;

    public bool IsCreating { get; private set; }

    public bool IsCancelling { get; private set; }

    public WorkspaceCreationProgress? Progress { get; private set; }

    public WorkspaceCreationException? Failure { get; set; }

    public WorkspaceCreationResult? Created { get; private set; }

    /// <summary>
    /// Raised when anything about the session changes. It can be raised on any thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Raised when the workspace was created without warnings, and its tab has been opened.
    /// </summary>
    public event Action<Workspace>? Completed;

    public async Task CreateAsync()
    {
        if (!Draft.IsLocationValid)
        {
            // Something appeared at the Workspace Folder since the last step.
            Step = NewWorkspaceStep.NameAndLocation;
            return;
        }

        Failure = null;
        Progress = null;
        IsCreating = true;
        IsCancelling = false;
        using var creation = _creation = new CancellationTokenSource();
        var progress = new Progress<WorkspaceCreationProgress>(update =>
        {
            Progress = update;
            Changed?.Invoke();
        });
        Changed?.Invoke();
        try
        {
            Created = await creator.CreateAsync(Draft.ToCreationRequest(), progress, creation.Token);
            if (Created.Warnings.Count == 0)
            {
                OpenCreated();
            }
        }
        catch (WorkspaceCreationException ex)
        {
            Failure = ex;
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the user, or by closing the tab. Nothing is left on disk.
        }
        finally
        {
            _creation = null;
            IsCreating = false;
            Changed?.Invoke();
        }
    }

    public void CancelCreating()
    {
        IsCancelling = true;
        _creation?.Cancel();
        Changed?.Invoke();
    }

    /// <summary>
    /// Opens the created workspace's tab.
    /// </summary>
    public void OpenCreated()
    {
        if (Created?.Workspace is { } workspace)
        {
            tabs.Open(workspace.Id, workspace.Name);
            Completed?.Invoke(workspace);
        }
    }
}
