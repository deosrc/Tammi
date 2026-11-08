namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Options for <see cref="IFilePicker.PickFileAsync"/>. Platforms that can't apply an option ignore it.
/// </summary>
public sealed record FilePickerOptions
{
    /// <summary>
    /// The dialog's title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The folder the dialog starts in.
    /// </summary>
    public string? InitialDirectory { get; init; }

    /// <summary>
    /// What the files that can be chosen are, e.g. "Images", shown with <see cref="Extensions"/>.
    /// </summary>
    public string? FileTypeName { get; init; }

    /// <summary>
    /// The extensions of the files that can be chosen, each with its dot (e.g. <c>.png</c>), or empty for any file.
    /// </summary>
    public IReadOnlyList<string> Extensions { get; init; } = [];
}
