using System;
using MouseMover;
using Xunit;

public class StopPolicyTests
{
    // 2026-06-15는 월요일. 근무 09:00~18:00, 월~금 기본.
    private static Settings WorkdaySchedule() => new()
    {
        ScheduleEnabled = true,
        WorkStartMinutes = 540,   // 09:00
        WorkEndMinutes = 1080,    // 18:00
        WorkDays = new[] { false, true, true, true, true, true, false }
    };

    private static DateTime Mon(int hour, int min) => new(2026, 6, 15, hour, min, 0); // 월요일
    private static DateTime Sun(int hour, int min) => new(2026, 6, 14, hour, min, 0); // 일요일

    // --- 자동종료 타이머 ---
    [Fact]
    public void AutoOff_not_reached_returns_false()
    {
        var s = new Settings { AutoOffMinutes = 60 };
        var start = Mon(10, 0);
        Assert.False(StopPolicy.ShouldAutoStop(s, start, start.AddMinutes(59)));
    }

    [Fact]
    public void AutoOff_reached_returns_true()
    {
        var s = new Settings { AutoOffMinutes = 60 };
        var start = Mon(10, 0);
        Assert.True(StopPolicy.ShouldAutoStop(s, start, start.AddMinutes(60)));
    }

    [Fact]
    public void AutoOff_zero_never_triggers()
    {
        var s = new Settings { AutoOffMinutes = 0 };
        var start = Mon(10, 0);
        Assert.False(StopPolicy.ShouldAutoStop(s, start, start.AddHours(10)));
    }

    // --- 스케줄: 전환에만 발동 ---
    [Fact]
    public void Schedule_started_in_work_now_after_work_stops()
    {
        var s = WorkdaySchedule();
        Assert.True(StopPolicy.ShouldAutoStop(s, Mon(17, 0), Mon(18, 0)));
    }

    [Fact]
    public void Schedule_started_in_work_still_in_work_does_not_stop()
    {
        var s = WorkdaySchedule();
        Assert.False(StopPolicy.ShouldAutoStop(s, Mon(10, 0), Mon(11, 0)));
    }

    [Fact]
    public void Schedule_started_outside_work_does_not_stop()
    {
        var s = WorkdaySchedule();
        // 비근무(20:00) 시작 → 스케줄은 끄지 않음
        Assert.False(StopPolicy.ShouldAutoStop(s, Mon(20, 0), Mon(23, 0)));
    }

    [Fact]
    public void Schedule_disabled_does_not_stop()
    {
        var s = WorkdaySchedule();
        s.ScheduleEnabled = false;
        Assert.False(StopPolicy.ShouldAutoStop(s, Mon(17, 0), Mon(18, 0)));
    }

    [Fact]
    public void Schedule_invalid_window_does_not_stop()
    {
        var s = WorkdaySchedule();
        s.WorkStartMinutes = 1080; // 종료 <= 시작 → 무효
        s.WorkEndMinutes = 540;
        Assert.False(StopPolicy.ShouldAutoStop(s, Mon(10, 0), Mon(19, 0)));
    }

    // --- IsWorkTime ---
    [Fact]
    public void IsWorkTime_boundary_end_is_not_work()
    {
        var s = WorkdaySchedule();
        Assert.True(StopPolicy.IsWorkTime(s, Mon(9, 0)));    // 시작 포함
        Assert.False(StopPolicy.IsWorkTime(s, Mon(18, 0)));  // 종료 제외
        Assert.False(StopPolicy.IsWorkTime(s, Mon(8, 59)));
    }

    [Fact]
    public void IsWorkTime_disabled_weekday_is_not_work()
    {
        var s = WorkdaySchedule();
        Assert.False(StopPolicy.IsWorkTime(s, Sun(10, 0))); // 일요일 off
    }

    [Fact]
    public void IsWorkTime_short_workdays_array_returns_false_without_throwing()
    {
        var s = WorkdaySchedule();
        s.WorkDays = new[] { true, true }; // shorter than 7
        Assert.False(StopPolicy.IsWorkTime(s, Mon(10, 0)));
    }
}
