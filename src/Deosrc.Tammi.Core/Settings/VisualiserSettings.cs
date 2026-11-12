using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// The settings of the visualiser.
/// </summary>
public sealed record VisualiserSettings
{
    /// <summary>
    /// How the visualiser draws each workspace.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<WorkspaceVisualisationType>))]
    public WorkspaceVisualisationType WorkspaceVisualisation { get; init; } = WorkspaceVisualisationType.Plasma;

    /// <summary>
    /// Replaces values this version of Tammi doesn't know with the default.
    /// </summary>
    public VisualiserSettings Normalize() => this with
    {
        WorkspaceVisualisation = Enum.IsDefined(WorkspaceVisualisation)
            ? WorkspaceVisualisation
            : WorkspaceVisualisationType.Plasma,
    };
}
