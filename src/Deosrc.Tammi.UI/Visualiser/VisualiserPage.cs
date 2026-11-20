namespace Deosrc.Tammi.UI.Visualiser;

/// <summary>
/// The visualiser's page: a canvas and the script that draws on it. It has no other files, so a host can show it
/// from a string.
/// </summary>
public static class VisualiserPage
{
    /// <summary>
    /// The page's HTML.
    /// </summary>
    public static string Html { get; } = Read();

    private static string Read()
    {
        using var stream = typeof(VisualiserPage).Assembly
            .GetManifestResourceStream("Deosrc.Tammi.UI.Visualiser.visualiser.html")
            ?? throw new InvalidOperationException("The visualiser page isn't embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
