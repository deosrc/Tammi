using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// One run of the Edit Workspace flow. It outlives the page, so switching to another tab and back resumes it, and a
/// save that's running carries on meanwhile.
/// </summary>
public sealed class EditWorkspaceSession(
    Workspace workspace, WorkspaceDraft draft, WorkspaceEditor editor)
{
    private CancellationTokenSource? _save;

    public Workspace Workspace { get; } = workspace;

    public WorkspaceDraft Draft { get; } = draft;

    public EditWorkspaceStep Step { get; set; } = EditWorkspaceStep.Tasks;

    public bool IsSaving { get; private set; }

    public bool IsCancelling { get; private set; }

    public WorkspaceCreationProgress? Progress { get; private set; }

    public WorkspaceEditException? Failure { get; set; }

    public WorkspaceCreationResult? Saved { get; private set; }

    /// <summary>
    /// Raised when anything about the session changes. It can be raised on any thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Raised when the changes were saved without warnings.
    /// </summary>
    public event Action? Completed;

    public async Task SaveAsync()
    {
        Failure = null;
        Progress = null;
        IsSaving = true;
        IsCancelling = false;
        using var save = _save = new CancellationTokenSource();
        var progress = new Progress<WorkspaceCreationProgress>(update =>
        {
            Progress = update;
            Changed?.Invoke();
        });
        Changed?.Invoke();
        try
        {
            Saved = await editor.EditAsync(Workspace.Id, Draft.ToEditRequest(), progress, save.Token);
            if (Saved.Warnings.Count == 0)
            {
                Completed?.Invoke();
            }
        }
        catch (WorkspaceEditException ex)
        {
            Failure = ex;
        }
        catch (WorkspaceException ex)
        {
            Failure = new WorkspaceEditException(ex.Message, ex.Folder, innerException: ex);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the user, or by closing the workspace's tab. Whatever was added has been removed.
        }
        finally
        {
            _save = null;
            IsSaving = false;
            Changed?.Invoke();
        }
    }

    public void CancelSaving()
    {
        IsCancelling = true;
        _save?.Cancel();
        Changed?.Invoke();
    }
}
