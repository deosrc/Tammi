using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Deosrc.Tammi.UI.Components;

/// <summary>
/// A <see cref="FluentTextInput"/> to use with <c>Autofocus</c>. Fluent focuses the input after its first render, but
/// if the page has gone by then, e.g. Home when the app starts and goes straight to the tab shown last, its element no
/// longer exists. Fluent's focus then fails with "Unable to focus an invalid element", and the failure would end the UI.
/// This input ignores that failure, as there's nothing left to focus.
/// </summary>
public class AutofocusTextInput(LibraryConfiguration configuration) : FluentTextInput(configuration)
{
    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await base.OnAfterRenderAsync(firstRender);
        }
        catch (JSException ex) when (firstRender && Autofocus && IsMissingElement(ex))
        {
            // The input was removed before it could be focused.
        }
    }

    private static bool IsMissingElement(JSException ex) =>
        ex.Message.StartsWith("Unable to focus an invalid element", StringComparison.Ordinal);
}
