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

    private const string SystemDirectory = @"C:\Windows\System32";
    private const string Batch = @"C:\npm\claude.cmd";
    private const string BatchPrefix = @"cmd.exe /e:ON /v:OFF /d /c """"C:\npm\claude.cmd""";

    [Theory]
    [InlineData(@"C:\tools\app.exe")]
    [InlineData(@"C:\tools\app.COM")]
    public void Create_ForProgram_StartsItDirectly(string fileName)
    {
        var (applicationName, commandLine) = WindowsCommandLine.Create(fileName, ["a & b"], SystemDirectory);

        Assert.Equal(fileName, applicationName);
        Assert.Equal(fileName + @" ""a & b""", commandLine);
    }

    [Theory]
    [InlineData(@"C:\npm\claude.cmd")]
    [InlineData(@"C:\npm\CLAUDE.CMD")]
    [InlineData(@"C:\tools\run.bat")]
    public void Create_ForBatchFile_RunsItThroughSystemCmd(string fileName)
    {
        var (applicationName, commandLine) = WindowsCommandLine.Create(fileName, ["x"], SystemDirectory);

        Assert.Equal(Path.Combine(SystemDirectory, "cmd.exe"), applicationName);
        Assert.StartsWith(@"cmd.exe /e:ON /v:OFF /d /c """"" + fileName + @"""", commandLine, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new string[0], "")]
    [InlineData(new[] { "plain" }, " plain")]
    [InlineData(new[] { "--flag", "C:/a/b.txt", @"C:\a\b" }, @" --flag C:/a/b.txt C:\a\b")]
    [InlineData(new[] { "" }, @" """"")]
    [InlineData(new[] { "two words" }, @" ""two words""")]
    [InlineData(new[] { "a&b|c<d>e^f" }, @" ""a&b|c<d>e^f""")]
    [InlineData(new[] { "(x) !y! ;z, =w" }, @" ""(x) !y! ;z, =w""")]
    [InlineData(new[] { "100%" }, @" ""100%%cd:~,%""")]
    [InlineData(new[] { "%PATH%" }, @" ""%%cd:~,%PATH%%cd:~,%""")]
    [InlineData(new[] { @"say ""hi""" }, @" ""say """"hi""""""")]
    [InlineData(new[] { @"a\""b" }, @" ""a\\""""b""")]
    [InlineData(new[] { @"C:\folder\" }, @" ""C:\folder\\""")]
    [InlineData(new[] { @"C:\some folder\" }, @" ""C:\some folder\\""")]
    [InlineData(new[] { "café" }, " café")]
    [InlineData(new[] { "tab\there" }, " \"tab\there\"")]
    public void BuildForBatchFile_QuotesAndEscapesArguments(string[] arguments, string expected)
    {
        Assert.Equal(BatchPrefix + expected + @"""", WindowsCommandLine.BuildForBatchFile(Batch, arguments));
    }

    [Fact]
    public void BuildForBatchFile_EscapesDefaultPromptAndPath()
    {
        var prompt = TAMMi.Core.Agents.InitialPrompt.Expand(
            "Read `{contextFile}` & go!", @"C:\Users\Jo\100% done\.tammi\tasks.md");

        Assert.Equal(
            BatchPrefix + @" ""Read `C:\Users\Jo\100%%cd:~,% done\.tammi\tasks.md` & go!""""",
            WindowsCommandLine.BuildForBatchFile(Batch, [prompt]));
    }

    [Theory]
    [InlineData("line one\nline two")]
    [InlineData("line one\r\nline two")]
    [InlineData("carriage\rreturn")]
    public void Create_ForBatchFile_RejectsLineBreaks(string argument)
    {
        Assert.Throws<ArgumentException>(() => WindowsCommandLine.Create(Batch, [argument], SystemDirectory));
    }

    [Fact]
    public void Create_ForProgram_KeepsLineBreaks()
    {
        var (_, commandLine) = WindowsCommandLine.Create(@"C:\tools\app.exe", ["one\ntwo"], SystemDirectory);

        Assert.Equal("C:\\tools\\app.exe \"one\ntwo\"", commandLine);
    }
}
