namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// An initial prompt template that Tammi offers on the Settings screen, so the user can choose what an agent does once
/// it has read the task context file (see <see cref="InitialPrompt.Predefined"/>).
/// </summary>
/// <param name="Name">What the prompt has the agent do, e.g. "Plan the change".</param>
/// <param name="Description">A sentence that says more about it.</param>
/// <param name="Template">The template, with <see cref="InitialPrompt.ContextFilePlaceholder"/>.</param>
public sealed record PredefinedPrompt(string Name, string Description, string Template);
