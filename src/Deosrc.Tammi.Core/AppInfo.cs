using System.Reflection;

namespace Deosrc.Tammi.Core;

/// <summary>
/// General information about the application.
/// </summary>
public static class AppInfo
{
    /// <summary>
    /// The product name shown in the UI.
    /// </summary>
    public const string Name = "Tammi";

    /// <summary>
    /// The product version shown in the UI, e.g. <c>0.1.0</c>. Build metadata such as the commit hash is dropped.
    /// </summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var version = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "";
        var metadata = version.IndexOf('+');
        return metadata < 0 ? version : version[..metadata];
    }
}
