namespace TAMMi.Core.Workspaces;

/// <summary>
/// How far <see cref="WorkspaceCreator"/> has got.
/// </summary>
/// <param name="Message">What it's doing now, e.g. "Fetching api…".</param>
/// <param name="CompletedSteps">How many steps are done.</param>
/// <param name="TotalSteps">How many steps there are in all.</param>
public sealed record WorkspaceCreationProgress(string Message, int CompletedSteps, int TotalSteps);
