using DeadlockTimers.Core.Sync;

namespace DeadlockTimers.Tests;

public class ClockTextParserTests
{
    private static TimeSpan Parse(string raw)
    {
        Assert.True(ClockTextParser.TryParse(raw, out var value));
        return value;
    }

    [Theory]
    [InlineData("12:34", "00:12:34")]
    [InlineData("12.34", "00:12:34")]
    [InlineData("1 2:34", "00:12:34")]
    [InlineData("O5:O7", "00:05:07")]
    [InlineData("l2:34", "00:12:34")]
    public void Parses_Plausible_Ocr_Text(string raw, string expected)
    {
        Assert.Equal(TimeSpan.Parse(expected), Parse(raw));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12:75")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_Unusable_Text(string? raw)
    {
        Assert.False(ClockTextParser.TryParse(raw, out _));
    }
}
