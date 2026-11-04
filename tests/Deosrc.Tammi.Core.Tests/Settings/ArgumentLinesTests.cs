using Tammi.Core.Settings;

namespace Tammi.Core.Tests.Settings;

public class ArgumentLinesTests
{
    [Fact]
    public void Format_PutsEachArgumentOnItsOwnLine()
    {
        Assert.Equal("--flag\nvalue with spaces\n\"quoted\"", ArgumentLines.Format(["--flag", "value with spaces", "\"quoted\""]));
    }

    [Fact]
    public void Format_WithoutArguments_IsEmpty()
    {
        Assert.Equal("", ArgumentLines.Format([]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n \r\n\t")]
    public void Parse_WithoutArguments_IsEmpty(string? text)
    {
        Assert.Empty(ArgumentLines.Parse(text));
    }

    [Fact]
    public void Parse_ReadsOneArgumentFromEachLine_AsItIs()
    {
        Assert.Equal(
            ["--flag", " value with spaces ", "\"quoted\"", "{prompt}"],
            ArgumentLines.Parse("--flag\r\n value with spaces \n\n\"quoted\"\r{prompt}\n"));
    }

    [Fact]
    public void Parse_ReadsWhatFormatWrites()
    {
        string[] arguments = ["-c", "echo 'a b'", "--prompt={prompt}"];

        Assert.Equal(arguments, ArgumentLines.Parse(ArgumentLines.Format(arguments)));
    }
}
