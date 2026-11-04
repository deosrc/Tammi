using Microsoft.AspNetCore.Components;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.UI.Pages.Settings;

/// <summary>
/// A Settings section that edits the <see cref="AppSettings"/>. Changes are saved as soon as they're made.
/// </summary>
public abstract class SettingsSectionBase : ComponentBase
{
    [Inject]
    protected ISettingsStore SettingsStore { get; set; } = null!;

    /// <summary>
    /// Why the last change couldn't be saved, or <c>null</c> if it was saved.
    /// </summary>
    protected string? SaveError { get; private set; }

    /// <summary>
    /// Changes the settings and saves them, recording any problem in <see cref="SaveError"/>.
    /// </summary>
    protected async Task UpdateAsync(Func<AppSettings, AppSettings> update)
    {
        try
        {
            await SettingsStore.UpdateAsync(update);
            SaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = $"Couldn't save the settings. {ex.Message}";
        }
    }
}
