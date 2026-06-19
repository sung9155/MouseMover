namespace MouseMover;

public static class StopPolicy
{
    public static bool ShouldAutoStop(Settings s, DateTime startLocal, DateTime nowLocal)
    {
        if (s.AutoOffMinutes > 0 &&
            (nowLocal - startLocal).TotalMinutes >= s.AutoOffMinutes)
        {
            return true;
        }

        if (s.ScheduleEnabled &&
            s.WorkStartMinutes < s.WorkEndMinutes &&
            IsWorkTime(s, startLocal) &&
            !IsWorkTime(s, nowLocal))
        {
            return true;
        }

        return false;
    }

    public static DateTime? NextAutoStop(Settings s, DateTime startLocal)
    {
        DateTime? next = null;

        if (s.AutoOffMinutes > 0)
            next = startLocal.AddMinutes(s.AutoOffMinutes);

        if (s.ScheduleEnabled &&
            s.WorkStartMinutes < s.WorkEndMinutes &&
            IsWorkTime(s, startLocal))
        {
            var workEnd = startLocal.Date.AddMinutes(s.WorkEndMinutes);
            if (next is null || workEnd < next) next = workEnd;
        }

        return next;
    }

    public static bool IsWorkTime(Settings s, DateTime t)
    {
        if (s.WorkStartMinutes >= s.WorkEndMinutes) return false;
        int day = (int)t.DayOfWeek;
        if (s.WorkDays.Length < 7 || day >= s.WorkDays.Length) return false;
        if (!s.WorkDays[day]) return false;
        int minutes = t.Hour * 60 + t.Minute;
        return s.WorkStartMinutes <= minutes && minutes < s.WorkEndMinutes;
    }
}
