using MouseMover;
using Xunit;

public class SettingsTests
{
    [Fact]
    public void Defaults_are_as_specified()
    {
        var s = new Settings();
        Assert.Equal(45, s.JiggleSeconds);
        Assert.Equal("절전방지 중", s.StatusText);
        Assert.True(s.ShowElapsed);
        Assert.True(s.ShowDismissHint);
        Assert.Equal(11f, s.LabelFontSize);
        Assert.Equal(unchecked((int)0xFFA0A0A0), s.LabelColorArgb);
        Assert.False(s.RunAtStartup);
    }

    [Fact]
    public void ToJson_then_FromJson_round_trips()
    {
        var s = new Settings
        {
            JiggleSeconds = 30,
            StatusText = "자리비움",
            ShowElapsed = false,
            ShowDismissHint = false,
            LabelFontSize = 20f,
            LabelColorArgb = unchecked((int)0xFF112233),
            RunAtStartup = true
        };
        var back = Settings.FromJson(s.ToJson());
        Assert.Equal(30, back.JiggleSeconds);
        Assert.Equal("자리비움", back.StatusText);
        Assert.False(back.ShowElapsed);
        Assert.False(back.ShowDismissHint);
        Assert.Equal(20f, back.LabelFontSize);
        Assert.Equal(unchecked((int)0xFF112233), back.LabelColorArgb);
        Assert.True(back.RunAtStartup);
    }

    [Fact]
    public void FromJson_invalid_returns_defaults()
    {
        var s = Settings.FromJson("this is not json");
        Assert.Equal(45, s.JiggleSeconds);
        Assert.Equal("절전방지 중", s.StatusText);
    }

    [Fact]
    public void FromJson_partial_keeps_defaults_for_missing()
    {
        var s = Settings.FromJson("{\"JiggleSeconds\":90}");
        Assert.Equal(90, s.JiggleSeconds);
        Assert.Equal("절전방지 중", s.StatusText); // 누락 필드는 기본값
        Assert.True(s.ShowElapsed);
    }
}
