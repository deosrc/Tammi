using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Deosrc.Tammi.UI.Components;

/// <summary>
/// Puts text on the system clipboard from the web view, and says it was copied.
/// </summary>
public sealed class Clipboard(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    /// <returns>Whether it was copied.</returns>
    public Task<bool> CopyAsync(string text) => CopyAsync("copyText", text);

    /// <summary>
    /// Copies the text an element shows, as it's laid out, to the clipboard.
    /// </summary>
    /// <returns>Whether it was copied.</returns>
    public Task<bool> CopyTextOfAsync(ElementReference element) => CopyAsync("copyElementText", element);

    /// <summary>
    /// Shows "Copied!" in a tooltip above an element for a moment.
    /// </summary>
    public Task ShowCopiedAsync(ElementReference element) => ShowCopiedOnAsync(element);

    /// <summary>
    /// Shows "Copied!" in a tooltip above the element with the given id for a moment.
    /// </summary>
    public Task ShowCopiedAsync(string elementId) => ShowCopiedOnAsync(elementId);

    private async Task<bool> CopyAsync(string identifier, object argument)
    {
        try
        {
            return await (await ModuleAsync()).InvokeAsync<bool>(identifier, argument);
        }
        catch (JSException)
        {
            return false;
        }
    }

    private async Task ShowCopiedOnAsync(object target)
    {
        try
        {
            await (await ModuleAsync()).InvokeVoidAsync("showCopied", target);
        }
        catch (JSException)
        {
            // The text was copied: only the tooltip is missing.
        }
    }

    private async ValueTask<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/Deosrc.Tammi.UI/js/clipboard.js");

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
