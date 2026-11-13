namespace Deosrc.Tammi.Installation;

/// <summary>
/// Whether a release archive bundles the .NET runtime.
/// </summary>
public enum ReleaseFlavour
{
    /// <summary>
    /// Needs the .NET runtime to be installed.
    /// </summary>
    FrameworkDependent,

    /// <summary>
    /// Bundles the .NET runtime.
    /// </summary>
    SelfContained,
}
