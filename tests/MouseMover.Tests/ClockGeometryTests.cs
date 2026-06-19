using System;
using MouseMover;
using Xunit;

public class ClockGeometryTests
{
    private static DateTime At(int h, int m, int s) => new(2026, 6, 19, h, m, s);

    [Fact] public void Hour_3() => Assert.Equal(90.0, ClockGeometry.HourAngle(At(3, 0, 0)), 3);
    [Fact] public void Hour_9() => Assert.Equal(270.0, ClockGeometry.HourAngle(At(9, 0, 0)), 3);
    [Fact] public void Hour_12() => Assert.Equal(0.0, ClockGeometry.HourAngle(At(12, 0, 0)), 3);
    [Fact] public void Hour_630() => Assert.Equal(195.0, ClockGeometry.HourAngle(At(6, 30, 0)), 3);
    [Fact] public void Minute_30() => Assert.Equal(180.0, ClockGeometry.MinuteAngle(At(1, 30, 0)), 3);
    [Fact] public void Minute_15() => Assert.Equal(90.0, ClockGeometry.MinuteAngle(At(1, 15, 0)), 3);
    [Fact] public void Second_15() => Assert.Equal(90.0, ClockGeometry.SecondAngle(At(1, 0, 15)), 3);
}
