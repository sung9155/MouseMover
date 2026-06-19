namespace MouseMover;

public static class ClockGeometry
{
    public static double HourAngle(DateTime t) => (t.Hour % 12 + t.Minute / 60.0) * 30.0;
    public static double MinuteAngle(DateTime t) => (t.Minute + t.Second / 60.0) * 6.0;
    public static double SecondAngle(DateTime t) => t.Second * 6.0;
}
