using System.Globalization;

namespace MouseMover;

public static class ClockFormat
{
    private static readonly CultureInfo Ko = new("ko-KR");

    public static string Text(DateTime t, bool seconds, bool twelveHour)
    {
        string fmt = twelveHour
            ? (seconds ? "tt h:mm:ss" : "tt h:mm")
            : (seconds ? "HH:mm:ss" : "HH:mm");
        return t.ToString(fmt, Ko);
    }
}
