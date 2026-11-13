using Deosrc.Tammi.Core.Installation;

namespace Deosrc.Tammi.Core.Tests.Installation;

public class DependencyCheckerTests
{
    private const string Runtimes = """
        Microsoft.AspNetCore.App 10.0.1 [/usr/share/dotnet/shared/Microsoft.AspNetCore.App]
        Microsoft.NETCore.App 8.0.11 [/usr/share/dotnet/shared/Microsoft.NETCore.App]
        """;

    [Fact]
    public void RuntimeIsMissingWhenOnlyAnotherMajorVersionIsListed() =>
        Assert.False(DependencyChecker.HasRuntime(Runtimes, 10));

    [Fact]
    public void FindsTheRuntime() =>
        Assert.True(DependencyChecker.HasRuntime(
            Runtimes + "\nMicrosoft.NETCore.App 10.0.2 [/usr/share/dotnet/shared/Microsoft.NETCore.App]\n", 10));

    [Fact]
    public void MajorVersionTenIsNotMatchedByOneHundred() =>
        Assert.False(DependencyChecker.HasRuntime("Microsoft.NETCore.App 100.0.0 [/x]", 10));
}
