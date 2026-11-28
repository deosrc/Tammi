using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.UI.Components;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// How a Task Status is shown: its colour and the words for its category, the same wherever a task's status appears.
/// </summary>
public static class TaskStatusText
{
    /// <summary>
    /// The colour for a status in <paramref name="category"/>: blue in progress, green done, otherwise neutral.
    /// </summary>
    public static SidePanelCardTone Tone(TaskStatusCategory? category) => category switch
    {
        TaskStatusCategory.InProgress => SidePanelCardTone.Info,
        TaskStatusCategory.Done => SidePanelCardTone.Success,
        _ => SidePanelCardTone.Neutral,
    };

    /// <summary>
    /// The category's name, e.g. "In progress", or <see langword="null"/> if the source didn't say.
    /// </summary>
    public static string? CategoryText(TaskStatusCategory? category) => category switch
    {
        TaskStatusCategory.ToDo => "To do",
        TaskStatusCategory.InProgress => "In progress",
        TaskStatusCategory.Done => "Done",
        _ => null,
    };
}
