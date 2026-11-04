using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Tests.Settings;

public class CommandLinePreviewTests
{
    [Fact]
    public void Format_JoinsCommandAndArgumentsWithSpaces()
    {
        Assert.Equal("agent --flag value", CommandLinePreview.Format("agent", ["--flag", "value"]));
    }

    [Fact]
    public void Format_QuotesArgumentsThatAreEmptyOrHaveSpacesOrQuotes()
    {
        Assert.Equal(
            "\"/opt/my agent\" \"\" \"a b\" \"say \\\"hi\\\"\" --prompt=x",
            CommandLinePreview.Format("/opt/my agent", ["", "a b", "say \"hi\"", "--prompt=x"]));
    }
}
