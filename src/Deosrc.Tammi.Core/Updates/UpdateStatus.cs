namespace Deosrc.Tammi.Core.Updates;

/// <summary>
/// Where <see cref="AppUpdater"/> is.
/// </summary>
public enum UpdateStatus
{
    /// <summary>
    /// Not doing anything, and no update is waiting.
    /// </summary>
    Idle,

    /// <summary>
    /// Looking for a newer release.
    /// </summary>
    Checking,

    /// <summary>
    /// A newer release is available and the user hasn't answered yet.
    /// </summary>
    Available,

    /// <summary>
    /// Downloading and installing the newer release.
    /// </summary>
    Installing,

    /// <summary>
    /// The newer release is installed and the menu link points to it. It's used the next time Tammi starts.
    /// </summary>
    Installed,

    /// <summary>
    /// Checking or installing failed. See <see cref="AppUpdater.Error"/>.
    /// </summary>
    Failed,
}
