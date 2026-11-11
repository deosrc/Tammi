using Deosrc.Tammi.Core.Usage;
using Xunit;

namespace Deosrc.Tammi.Core.Tests.Usage;

public class ClaudeUsageProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parse_ReadsLimitsInOrder_AndSkipsNullOnes()
    {
        const string json = """
            {
              "five_hour": { "utilization": 37.5, "resets_at": "2026-11-11T15:00:00+00:00" },
              "seven_day": { "utilization": 120, "resets_at": null },
              "seven_day_opus": null
            }
            """;

        var usage = ClaudeUsageProvider.Parse(json, Now);

        Assert.Equal(["5h", "Weekly"], usage.Limits.Select(limit => limit.Name));
        Assert.Equal(37.5, usage.Limits[0].PercentUsed);
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 15, 0, 0, TimeSpan.Zero), usage.Limits[0].ResetsAt);
        Assert.Equal(100, usage.Limits[1].PercentUsed);
        Assert.Null(usage.Limits[1].ResetsAt);
    }

    [Fact]
    public void Parse_RejectsInvalidJson() =>
        Assert.Throws<AgentUsageException>(() => ClaudeUsageProvider.Parse("not json", Now));
}
