namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// What <see cref="WorkspaceTabRestorer.RestoreAsync"/> did.
/// </summary>
/// <param name="Problems">Why tabs couldn't be opened again, one message for each.</param>
/// <param name="Active">The tab to show, or <c>null</c> to leave the view as it is.</param>
/// <param name="Restored">Whether the tabs were restored now, rather than already tracked.</param>
public sealed record WorkspaceTabRestoration(IReadOnlyList<string> Problems, IMainAreaTab? Active, bool Restored);
