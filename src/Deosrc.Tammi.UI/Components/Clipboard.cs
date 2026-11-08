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
    public async Task<bool> CopyAsync(string text)
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Deosrc.Tammi.UI/js/clipboard.js");
            return await _module.InvokeAsync<bool>("copyText", text);
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
