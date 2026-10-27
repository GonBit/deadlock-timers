using DeadlockTimers.Core.Config;

namespace DeadlockTimers.Tests;

public class TimeParsingTests
{
    [Fact]
    public void Parses_3_00_As_3_Minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(3), TimeParsing.ParseMinSec("3:00"));
    }

    [Fact]
    public void Parses_10_00_As_10_Minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), TimeParsing.ParseMinSec("10:00"));
    }

    [Fact]
    public void Parses_0_45_As_45_Seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(45), TimeParsing.ParseMinSec("0:45"));
    }

    [Fact]
    public void Throws_On_Seconds_Out_Of_Range()
    {
        Assert.Throws<FormatException>(() => TimeParsing.ParseMinSec("3:60"));
    }

    [Fact]
    public void Throws_On_Garbage()
    {
        Assert.Throws<FormatException>(() => TimeParsing.ParseMinSec("abc"));
    }
}
