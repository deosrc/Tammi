using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Deosrc.Tammi.UI.Components;

/// <summary>
/// Puts text on the system clipboard from the web view.
/// </summary>
public sealed class Clipboard(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    /// <returns>Whether it was copied.</returns>
    public Task<bool> CopyAsync(string text) => InvokeAsync("copyText", text);

    /// <summary>
    /// Copies the text an element shows, as it's laid out, to the clipboard.
    /// </summary>
    /// <returns>Whether it was copied.</returns>
    public Task<bool> CopyTextOfAsync(ElementReference element) => InvokeAsync("copyElementText", element);

    private async Task<bool> InvokeAsync(string identifier, object argument)
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Deosrc.Tammi.UI/js/clipboard.js");
            return await _module.InvokeAsync<bool>(identifier, argument);
        }
        catch (JSException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The web view has gone.
            }
        }
    }
}
