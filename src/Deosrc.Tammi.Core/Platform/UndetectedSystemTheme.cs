namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// An <see cref="ISystemTheme"/> for platforms that don't detect the system theme themselves.
/// </summary>
public sealed class UndetectedSystemTheme : ISystemTheme
{
    /// <inheritdoc />
    public bool? IsDark => null;

    /// <inheritdoc />
    public event Action? Changed
    {
        add { }
        remove { }
    }
}
