namespace TAMMi.Core.Platform;

/// <summary>
/// Options for <see cref="IFolderPicker.PickFolderAsync"/>. Platforms that can't apply an option ignore it.
/// </summary>
public sealed record FolderPickerOptions
{
    /// <summary>
    /// The dialog's title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The folder the dialog starts in.
    /// </summary>
    public string? InitialDirectory { get; init; }
}
