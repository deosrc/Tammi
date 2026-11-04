using Microsoft.UI.Xaml.Controls;
using TAMMi.Core.Agents;
using TAMMi.UI.Workspaces;

namespace TAMMi.Windows.PlatformServices;

/// <summary>
/// Warns the user when they close the main window while agents are running, and lets them change their mind.
/// </summary>
internal static class CloseConfirmation
{
    /// <summary>
    /// Asks before the window closes, whenever agents are running. Call it when the window is created.
    /// </summary>
    public static void Attach(Microsoft.UI.Xaml.Window window, WorkspaceAgents agents)
    {
        var confirmed = false;
        var asking = false;
        window.AppWindow.Closing += async (_, args) =>
        {
            var running = agents.CountRunning();

            // Without a XAML root there's nowhere to ask, so the window closes as it would without agents.
            if (confirmed || running == 0 || window.Content?.XamlRoot is not { } root)
            {
                return;
            }

            // Cancelled before the dialog is awaited, so the window stays open while the user decides.
            args.Cancel = true;
            if (asking)
            {
                return;
            }

            asking = true;
            try
            {
                var dialog = new ContentDialog
                {
                    Title = AppCloseWarning.Title,
                    Content = AppCloseWarning.Message(running),
                    PrimaryButtonText = "Close TAMMi",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = root,
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    confirmed = true;
                    window.Close();
                }
            }
            finally
            {
                asking = false;
            }
        };
    }
}
