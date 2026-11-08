using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Tests.Integrations;

public sealed class TaskReferenceTests
{
    [Theory]
    [InlineData("acme/api#42", "api#42")]
    [InlineData("ABC-123", "ABC-123")]
    [InlineData("a/b/c#1", "c#1")]
    public void ShortKey_DropsTheOwner(string key, string expected)
    {
        Assert.Equal(expected, TaskReference.ShortKey(key));
    }
}
