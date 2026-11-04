namespace TAMMi.Core.Git;

/// <summary>
/// A detected git install.
/// </summary>
/// <param name="ExecutablePath">The executable that was run, e.g. <c>git</c>.</param>
/// <param name="Version">The numeric version, e.g. 2.43.0. Platform suffixes such as <c>.windows.1</c> are dropped.</param>
/// <param name="VersionText">The full version reported by <c>git --version</c>, e.g. <c>2.43.0.windows.1</c>.</param>
public sealed record GitInstallation(string ExecutablePath, Version Version, string VersionText);
