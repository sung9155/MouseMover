using MouseMover;
using Xunit;

public class TimeFormatTests
{
    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(5, "00:00:05")]
    [InlineData(65, "00:01:05")]
    [InlineData(3661, "01:01:01")]
    [InlineData(36000, "10:00:00")]
    public void Elapsed_formats_as_HHMMSS(int totalSeconds, string expected)
    {
        var result = TimeFormat.Elapsed(TimeSpan.FromSeconds(totalSeconds));
        Assert.Equal(expected, result);
    }
}
