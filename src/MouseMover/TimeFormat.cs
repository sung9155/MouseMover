namespace MouseMover;

public static class TimeFormat
{
    public static string Elapsed(TimeSpan span)
    {
        int totalHours = (int)span.TotalHours;
        return $"{totalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
    }
}
