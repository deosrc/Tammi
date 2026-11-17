using Deosrc.Tammi.Core.Installation;

namespace Deosrc.Tammi.Core.Tests.Installation;

public class DesktopEntryTests
{
    [Fact]
    public void ExecIsQuotedAndEscaped() =>
        Assert.Equal("\"/home/a b/$x/Deosrc.Tammi.Linux\"".Replace("$", "\\\\$"),
            DesktopEntry.QuoteExec("/home/a b/$x/Deosrc.Tammi.Linux"));

    [Fact]
    public void RendersAnApplicationEntry()
    {
        var text = DesktopEntry.Render("/data/tammi/versions/0.1.0/Deosrc.Tammi.Linux");

        Assert.StartsWith("[Desktop Entry]\n", text);
        Assert.Contains("Exec=\"/data/tammi/versions/0.1.0/Deosrc.Tammi.Linux\"\n", text);
        Assert.Contains("Name=Tammi\n", text);
    }
}
