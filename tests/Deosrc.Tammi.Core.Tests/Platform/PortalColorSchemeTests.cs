using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Tests.Platform;

public class PortalColorSchemeTests
{
    [Theory]
    [InlineData("(<<uint32 1>>,)\n", true)]
    [InlineData("(<<uint32 2>>,)\n", false)]
    [InlineData("(<<uint32 0>>,)\n", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("Error: no such method", null)]
    public void ParseIsDark_reads_the_scheme(string? output, bool? expected) =>
        Assert.Equal(expected, PortalColorScheme.ParseIsDark(output));
}
