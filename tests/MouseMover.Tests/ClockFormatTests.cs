using System;
using MouseMover;
using Xunit;

public class ClockFormatTests
{
    private static DateTime At(int h, int m, int s) => new(2026, 6, 19, h, m, s);

    [Fact] public void H24_no_seconds() => Assert.Equal("09:05", ClockFormat.Text(At(9, 5, 7), false, false));
    [Fact] public void H24_seconds() => Assert.Equal("09:05:07", ClockFormat.Text(At(9, 5, 7), true, false));
    [Fact] public void H24_afternoon() => Assert.Equal("13:05", ClockFormat.Text(At(13, 5, 0), false, false));

    [Fact] public void H12_morning() => Assert.Equal("오전 9:05", ClockFormat.Text(At(9, 5, 0), false, true));
    [Fact] public void H12_afternoon() => Assert.Equal("오후 1:05", ClockFormat.Text(At(13, 5, 0), false, true));
    [Fact] public void H12_midnight() => Assert.Equal("오전 12:30", ClockFormat.Text(At(0, 30, 0), false, true));
    [Fact] public void H12_seconds() => Assert.Equal("오후 1:05:09", ClockFormat.Text(At(13, 5, 9), true, true));
}
