using Deosrc.Tammi.Installation;

namespace Deosrc.Tammi.Core.Tests.Installation;

public class PackageVersionTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3-beta.1", "1.2.3-beta.1")]
    [InlineData("1.2.3+abc123", "1.2.3")]
    [InlineData("0.0.0-dev", "0.0.0-dev")]
    public void Parses(string text, string expected) =>
        Assert.Equal(expected, PackageVersion.Parse(text).ToString());

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("a.b.c")]
    [InlineData("1.2.3-")]
    public void RejectsInvalid(string text) => Assert.False(PackageVersion.TryParse(text, out _));

    [Theory]
    [InlineData("1.0.0", "2.0.0")]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("1.0.0-beta.1", "1.0.0")]
    [InlineData("1.0.0-alpha", "1.0.0-beta")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
    [InlineData("1.0.0-1", "1.0.0-alpha")]
    [InlineData("1.0.0-beta", "1.0.0-beta.1")]
    public void OrdersOlderBeforeNewer(string older, string newer)
    {
        Assert.True(PackageVersion.Parse(older) < PackageVersion.Parse(newer));
        Assert.True(PackageVersion.Parse(newer) > PackageVersion.Parse(older));
    }

    [Fact]
    public void IgnoresBuildMetadataWhenComparing() =>
        Assert.Equal(PackageVersion.Parse("1.0.0+a"), PackageVersion.Parse("1.0.0+b"));
}
