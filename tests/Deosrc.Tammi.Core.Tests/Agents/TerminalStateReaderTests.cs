using System.Text;
using Deosrc.Tammi.Core.Agents;

namespace Deosrc.Tammi.Core.Tests.Agents;

public class TerminalStateReaderTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void Read_FindsReportsEndedByBellOrStringTerminator()
    {
        var reports = new TerminalStateReader().Read(
            Bytes("hi \x1b]7501;state=working:id=ses_a1:app=opencode\x07 x \x1b]7501;state=idle\x1b\\ y"));

        Assert.Equal(
            [new TerminalStateReader.Report("working", "ses_a1"), new TerminalStateReader.Report("idle", null)],
            reports);
    }

    [Fact]
    public void Read_FindsReportsSplitAnywhere()
    {
        var reader = new TerminalStateReader();
        var bytes = Bytes("\x1b]7501;state=blocked:id=ses_b:kind=permission\x1b\\");
        var reports = new List<TerminalStateReader.Report>();
        foreach (var value in bytes)
        {
            reports.AddRange(reader.Read([value]));
        }

        Assert.Equal([new TerminalStateReader.Report("blocked", "ses_b")], reports);
    }

    [Theory]
    [InlineData("\x1b]7501;?\x07")]
    [InlineData("\x1b]7501;?:states=idle,working\x07")]
    [InlineData("\x1b]0;title\x07")]
    [InlineData("\x1b]7501;state=\x07")]
    [InlineData("\x1b[31mred\x1b[0m")]
    [InlineData("\x1b]7501;state=working")]
    public void Read_IgnoresEverythingElse(string output)
    {
        Assert.Empty(new TerminalStateReader().Read(Bytes(output)));
    }

    [Fact]
    public void Read_DropsTooLongSequences_AndRecovers()
    {
        var reader = new TerminalStateReader();

        Assert.Empty(reader.Read(Bytes("\x1b]7501;state=working:title=" + new string('a', 5000) + "\x07")));
        Assert.Equal([new TerminalStateReader.Report("idle", null)], reader.Read(Bytes("\x1b]7501;state=idle\x07")));
    }

    [Fact]
    public void Read_RecoversFromAbandonedSequences()
    {
        var reports = new TerminalStateReader().Read(Bytes("\x1b]7501;state=wor\x1bX\x1b\x1b]7501;state=idle\x07"));

        Assert.Equal([new TerminalStateReader.Report("idle", null)], reports);
    }
}
