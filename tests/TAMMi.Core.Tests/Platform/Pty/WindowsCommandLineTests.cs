using TAMMi.Core.Platform.Pty;

namespace TAMMi.Core.Tests.Platform.Pty;

public class WindowsCommandLineTests
{
    [Theory]
    [InlineData(new string[0], @"C:\tools\app.exe")]
    [InlineData(new[] { "-a", "b" }, @"C:\tools\app.exe -a b")]
    [InlineData(new[] { "two words" }, @"C:\tools\app.exe ""two words""")]
    [InlineData(new[] { "" }, @"C:\tools\app.exe """"")]
    [InlineData(new[] { @"say ""hi""" }, @"C:\tools\app.exe ""say \""hi\""""")]
    [InlineData(new[] { @"C:\some folder\" }, @"C:\tools\app.exe ""C:\some folder\\""")]
    [InlineData(new[] { @"a\\""b" }, @"C:\tools\app.exe ""a\\\\\""b""")]
    [InlineData(new[] { @"C:\no\spaces\" }, @"C:\tools\app.exe C:\no\spaces\")]
    public void Build_QuotesArguments(string[] arguments, string expected)
    {
        Assert.Equal(expected, WindowsCommandLine.Build(@"C:\tools\app.exe", arguments));
    }

    [Fact]
    public void Build_QuotesFileNameWithSpaces()
    {
        Assert.Equal(
            @"""C:\Program Files\PowerShell\7\pwsh.exe"" -NoLogo",
            WindowsCommandLine.Build(@"C:\Program Files\PowerShell\7\pwsh.exe", ["-NoLogo"]));
    }
}
